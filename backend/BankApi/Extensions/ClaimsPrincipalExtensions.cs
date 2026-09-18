using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BankApi.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Récupère l'id du client actuellement authentifié à partir des claims du JWT.</summary>
    public static Guid GetCustomerId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (sub is null || !Guid.TryParse(sub, out var customerId))
            throw new UnauthorizedAccessException("Token invalide : identifiant client introuvable.");

        return customerId;
    }
}
