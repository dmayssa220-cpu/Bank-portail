using Microsoft.ML;

namespace BankApi.Integrations.Fraud;

/// <summary>
/// Service de scoring de fraude. Entraîne un modèle de classification binaire (régression
/// logistique via SDCA — léger, sans dépendance native supplémentaire, adapté à un conteneur
/// Docker sans configuration particulière) sur des données synthétiques générées au démarrage.
///
/// Enregistré en Singleton : l'entraînement (rapide, quelques centaines de ms pour ce volume)
/// n'a lieu qu'une seule fois, au premier démarrage de l'application.
/// </summary>
public class FraudDetectionService
{
    private readonly MLContext _mlContext = new(seed: 42);
    private readonly object _predictionLock = new();
    private readonly PredictionEngine<FraudFeatures, FraudPrediction> _engine;
    private readonly ILogger<FraudDetectionService> _logger;

    public FraudDetectionService(ILogger<FraudDetectionService> logger)
    {
        _logger = logger;

        var trainingData = SyntheticFraudDataGenerator.Generate(count: 3000);
        var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

        var pipeline = _mlContext.Transforms
            .Concatenate("Features",
                nameof(FraudFeatures.Amount),
                nameof(FraudFeatures.HourOfDay),
                nameof(FraudFeatures.IsNight),
                nameof(FraudFeatures.AmountToBalanceRatio))
            .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
            .Append(_mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(
                labelColumnName: nameof(FraudFeatures.Label),
                featureColumnName: "Features"));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var model = pipeline.Fit(dataView);
        stopwatch.Stop();

        _logger.LogInformation("Modèle de détection de fraude entraîné en {Ms} ms sur {Count} exemples synthétiques",
            stopwatch.ElapsedMilliseconds, trainingData.Count);

        _engine = _mlContext.Model.CreatePredictionEngine<FraudFeatures, FraudPrediction>(model);
    }

    /// <summary>
    /// Calcule un score de risque pour une opération donnée.
    /// </summary>
    /// <param name="amount">Montant de l'opération.</param>
    /// <param name="timestamp">Horodatage de l'opération (sert à détecter les opérations nocturnes).</param>
    /// <param name="accountBalanceBeforeOperation">Solde du compte avant l'opération.</param>
    public FraudScoreResult Score(decimal amount, DateTime timestamp, decimal accountBalanceBeforeOperation)
    {
        var hour = (float)timestamp.Hour;
        var isNight = (hour < 6 || hour >= 23) ? 1f : 0f;
        var ratio = accountBalanceBeforeOperation > 0
            ? (float)(amount / accountBalanceBeforeOperation)
            : 1f; // solde nul/négatif => on considère le risque comme maximal sur ce critère

        var input = new FraudFeatures
        {
            Amount = (float)amount,
            HourOfDay = hour,
            IsNight = isNight,
            AmountToBalanceRatio = Math.Min(ratio, 5f) // on plafonne pour éviter les valeurs extrêmes
        };

        FraudPrediction prediction;
        lock (_predictionLock) // PredictionEngine n'est pas thread-safe
        {
            prediction = _engine.Predict(input);
        }

        var reasons = new List<string>();
        if (amount > 3000) reasons.Add("Montant inhabituellement élevé");
        if (isNight == 1f) reasons.Add("Opération effectuée en dehors des heures habituelles");
        if (ratio > 0.5f) reasons.Add("Le montant représente une part importante du solde du compte");
        if (reasons.Count == 0) reasons.Add("Aucun facteur de risque particulier détecté");

        return new FraudScoreResult(prediction.Probability, prediction.IsFraud, reasons.ToArray());
    }
}
