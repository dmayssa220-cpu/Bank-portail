namespace BankApi.Models;

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Iban { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "TND";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
}
