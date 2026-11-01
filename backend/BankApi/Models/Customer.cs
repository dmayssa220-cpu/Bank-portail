namespace BankApi.Models;

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    // Authentification à deux facteurs (TOTP) : secret stocké tant que la 2FA n'est pas confirmée/activée
    public string? TwoFactorSecret { get; set; }
    public bool TwoFactorEnabled { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}
