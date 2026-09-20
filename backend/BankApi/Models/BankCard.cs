namespace BankApi.Models;

public class BankCard
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public string CardNumberMasked { get; set; } = string.Empty;
    public string CardHolderName { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public bool IsBlocked { get; set; }
    public decimal DailyLimit { get; set; } = 2000m;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
