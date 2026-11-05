using BankApi.Integrations.Fraud;

namespace BankApi.DTOs;

public record TransactionDto(Guid Id, string Type, decimal Amount, string? Label, DateTime CreatedAt);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public record DepositRequestDto(decimal Amount, string? Label);

public record TransferResultDto(
    Guid TransactionId,
    decimal NewBalance,
    bool IsInternal,
    string? BeneficiaryName,
    bool Replayed,
    FraudScoreResult? FraudAlert);
