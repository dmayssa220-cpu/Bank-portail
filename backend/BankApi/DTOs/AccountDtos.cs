namespace BankApi.DTOs;

public record AccountDto(Guid Id, string Iban, decimal Balance, string Currency, string CustomerName);

public record TransferRequestDto(Guid FromAccountId, string ToIban, decimal Amount, string? Label);

public record LoginRequestDto(string Email, string Password);

public record LoginResponseDto(string Token, string FullName, DateTime ExpiresAt);
