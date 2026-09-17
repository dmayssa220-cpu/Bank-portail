namespace BankApi.Models;

public enum TransactionType
{
    Deposit,
    Withdrawal,
    TransferOut,
    TransferIn
}

public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string? Label { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Clé d'idempotence : évite le double traitement d'une même opération
    public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString();
}
