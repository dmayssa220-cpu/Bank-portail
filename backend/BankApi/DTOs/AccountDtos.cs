namespace BankApi.DTOs;

public record AccountDto(Guid Id, string Iban, decimal Balance, string Currency, string CustomerName);

public record TransferRequestDto(Guid FromAccountId, string ToIban, decimal Amount, string? Label);

public record LoginRequestDto(string Email, string Password);

public record RegisterRequestDto(string FullName, string Email, string Password);

// Réponse de connexion. Deux cas possibles :
//  - 2FA désactivée (ou code 2FA validé) : Token/CustomerId/FullName/ExpiresAt renseignés.
//  - 2FA activée, 1re étape : RequiresTwoFactor = true + PendingToken (valable 5 min), pas encore de Token.
public record LoginResponseDto(
    string? Token,
    Guid? CustomerId,
    string? FullName,
    DateTime? ExpiresAt,
    bool RequiresTwoFactor = false,
    string? PendingToken = null);

public record LoginTwoFactorRequestDto(string PendingToken, string Code);

// --- Profil & sécurité ---
public record ProfileDto(string FullName, string Email, bool TwoFactorEnabled, DateTime CreatedAt);

public record UpdateProfileRequestDto(string FullName);

public record ChangePasswordRequestDto(string CurrentPassword, string NewPassword);

public record TwoFactorSetupResponseDto(string Secret, string OtpAuthUri);

public record TwoFactorCodeRequestDto(string Code);

public record DisableTwoFactorRequestDto(string Password);

public record CreateAccountRequestDto(string Currency);
