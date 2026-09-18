namespace BankApi.Integrations.Fraud;

/// <summary>
/// Génère un jeu de données synthétique pour entraîner le modèle.
/// Pas besoin de vraies données bancaires (qu'on n'a de toute façon pas sur un projet perso) :
/// on simule des transactions "normales" et des transactions "à risque" selon des règles
/// métier simples, avec un peu de bruit aléatoire pour que le modèle généralise.
/// </summary>
public static class SyntheticFraudDataGenerator
{
    public static List<FraudFeatures> Generate(int count, int seed = 42)
    {
        var random = new Random(seed);
        var data = new List<FraudFeatures>(count);

        for (int i = 0; i < count; i++)
        {
            // ~80% de transactions normales, ~20% de transactions "à risque" dans le jeu d'entraînement
            bool generateFraud = random.NextDouble() < 0.2;

            float amount;
            float hour;
            float ratio;

            if (generateFraud)
            {
                amount = (float)(1000 + random.NextDouble() * 9000); // montant élevé
                hour = (float)(random.NextDouble() < 0.6 ? random.Next(0, 6) : random.Next(22, 24)); // souvent la nuit
                ratio = (float)(0.4 + random.NextDouble() * 0.6); // grosse part du solde
            }
            else
            {
                amount = (float)(5 + random.NextDouble() * 800); // montant courant
                hour = (float)random.Next(7, 22); // heures normales
                ratio = (float)(random.NextDouble() * 0.35); // petite part du solde
            }

            var isNight = (hour < 6 || hour >= 23) ? 1f : 0f;

            // Un peu de bruit : ~5% des exemples ont une étiquette "inversée" pour éviter
            // que le modèle n'apprenne des règles trop rigides (sur-apprentissage).
            bool label = random.NextDouble() < 0.05 ? !generateFraud : generateFraud;

            data.Add(new FraudFeatures
            {
                Amount = amount,
                HourOfDay = hour,
                IsNight = isNight,
                AmountToBalanceRatio = ratio,
                Label = label
            });
        }

        return data;
    }
}
