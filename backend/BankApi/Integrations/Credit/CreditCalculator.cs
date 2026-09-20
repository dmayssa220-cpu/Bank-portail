namespace BankApi.Integrations.Credit;

public record CreditSimulationResult(
    decimal MonthlyPayment,
    decimal TotalCost,
    decimal TotalInterest,
    decimal AnnualRatePercent
);

/// <summary>
/// Calcul standard d'amortissement à mensualités constantes (formule utilisée par
/// la quasi-totalité des crédits à la consommation/immobiliers à taux fixe).
/// </summary>
public static class CreditCalculator
{
    public static CreditSimulationResult Simulate(decimal amount, int durationMonths, decimal annualRatePercent)
    {
        if (annualRatePercent <= 0)
        {
            var flatPayment = Math.Round(amount / durationMonths, 2);
            return new CreditSimulationResult(flatPayment, amount, 0, annualRatePercent);
        }

        var monthlyRate = (double)(annualRatePercent / 100m / 12m);
        var principal = (double)amount;

        // Formule : M = P * r / (1 - (1 + r)^-n)
        var monthlyPaymentRaw = principal * monthlyRate / (1 - Math.Pow(1 + monthlyRate, -durationMonths));
        var monthlyPayment = Math.Round((decimal)monthlyPaymentRaw, 2);

        var totalCost = Math.Round(monthlyPayment * durationMonths, 2);
        var totalInterest = Math.Round(totalCost - amount, 2);

        return new CreditSimulationResult(monthlyPayment, totalCost, totalInterest, annualRatePercent);
    }
}
