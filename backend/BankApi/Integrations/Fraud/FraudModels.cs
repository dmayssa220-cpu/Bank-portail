using Microsoft.ML.Data;

namespace BankApi.Integrations.Fraud;

/// <summary>Caractéristiques (features) utilisées par le modèle pour scorer une transaction.</summary>
public class FraudFeatures
{
    public float Amount { get; set; }
    public float HourOfDay { get; set; }
    public float IsNight { get; set; }
    public float AmountToBalanceRatio { get; set; }

    /// <summary>Utilisé uniquement pendant l'entraînement (données synthétiques).</summary>
    public bool Label { get; set; }
}

/// <summary>Résultat brut produit par le pipeline ML.NET.</summary>
public class FraudPrediction
{
    [ColumnName("PredictedLabel")]
    public bool IsFraud { get; set; }

    public float Probability { get; set; }

    public float Score { get; set; }
}

/// <summary>Résultat exposé par l'API, avec les raisons lisibles par un humain.</summary>
public record FraudScoreResult(float Probability, bool IsSuspicious, string[] Reasons);
