using OtpNet;

namespace BankApi.Integrations.TwoFactor;

/// <summary>
/// Authentification à deux facteurs de type TOTP (RFC 6238) — le même standard que Google
/// Authenticator, Microsoft Authenticator ou Authy (tous gratuits). Aucun SMS payant requis :
/// le code à 6 chiffres est calculé localement sur le téléphone de l'utilisateur à partir
/// d'un secret partagé, et change toutes les 30 secondes.
/// </summary>
public class TwoFactorService
{
    private const string Issuer = "Ma Banque";

    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20); // 160 bits, recommandation RFC 4226
        return Base32Encoding.ToString(key);
    }

    /// <summary>URI standard "otpauth://" que l'application d'authentification sait lire (via QR code).</summary>
    public string BuildOtpAuthUri(string email, string secret) =>
        $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(email)}" +
        $"?secret={secret}&issuer={Uri.EscapeDataString(Issuer)}&digits=6&period=30";

    public bool VerifyCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6) return false;

        var totp = new Totp(Base32Encoding.ToBytes(secret));

        // Fenêtre de tolérance de ±1 pas (30 s) pour absorber un léger décalage d'horloge du téléphone
        return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
    }
}
