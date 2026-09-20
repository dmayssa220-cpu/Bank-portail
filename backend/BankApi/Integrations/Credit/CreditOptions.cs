namespace BankApi.Integrations.Credit;

public class CreditOptions
{
    public const string SectionName = "Credit";

    /// <summary>Taux d'intérêt annuel appliqué (en %), fixe pour ce squelette pédagogique.</summary>
    public decimal AnnualRatePercent { get; set; } = 7.5m;

    public decimal MinAmount { get; set; } = 500;
    public decimal MaxAmount { get; set; } = 100000;
    public int MinDurationMonths { get; set; } = 6;
    public int MaxDurationMonths { get; set; } = 120;
}
