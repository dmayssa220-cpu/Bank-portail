namespace BankApi.Models;

public enum CreditRequestStatus
{
    Pending,
    Approved,
    Rejected
}

public class CreditRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public decimal Amount { get; set; }
    public int DurationMonths { get; set; }
    public decimal AnnualRate { get; set; }
    public decimal MonthlyPayment { get; set; }
    public decimal TotalCost { get; set; }

    public CreditRequestStatus Status { get; set; } = CreditRequestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
