namespace BankApi.DTOs;

// --- Crédit ---
public record CreditSimulationRequestDto(decimal Amount, int DurationMonths);

public record CreditSimulationResponseDto(
    decimal Amount,
    int DurationMonths,
    decimal AnnualRatePercent,
    decimal MonthlyPayment,
    decimal TotalCost,
    decimal TotalInterest
);

public record CreditRequestResponseDto(
    Guid Id,
    decimal Amount,
    int DurationMonths,
    decimal MonthlyPayment,
    decimal TotalCost,
    string Status,
    DateTime CreatedAt
);

// --- Cartes ---
public record CreateCardRequestDto(Guid AccountId, string? CardHolderName);

public record UpdateCardLimitDto(decimal DailyLimit);

public record CardDto(
    Guid Id,
    string CardNumberMasked,
    string CardHolderName,
    DateTime ExpiryDate,
    bool IsBlocked,
    decimal DailyLimit,
    string AccountIban
);
