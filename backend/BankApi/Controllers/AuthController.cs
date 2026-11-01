using BankApi.Data;
using BankApi.DTOs;
using BankApi.Integrations.Caching;
using BankApi.Integrations.TwoFactor;
using BankApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BankApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private static readonly TimeSpan PendingLoginTtl = TimeSpan.FromMinutes(5);

    private readonly BankDbContext _db;
    private readonly IConfiguration _config;
    private readonly RedisCacheService _redis;
    private readonly TwoFactorService _twoFactor;

    public AuthController(BankDbContext db, IConfiguration config, RedisCacheService redis, TwoFactorService twoFactor)
    {
        _db = db;
        _config = config;
        _redis = redis;
        _twoFactor = twoFactor;
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<ActionResult<LoginResponseDto>> Register([FromBody] RegisterRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Email et mot de passe sont obligatoires.");

        if (request.Password.Length < 6)
            return BadRequest("Le mot de passe doit contenir au moins 6 caractères.");

        var alreadyExists = await _db.Customers.AnyAsync(c => c.Email == request.Email);
        if (alreadyExists)
            return Conflict("Un compte existe déjà avec cet email.");

        var customer = new Customer
        {
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        var account = new Account
        {
            Iban = GenerateFakeIban(),
            Balance = 0,
            Currency = "TND",
            CustomerId = customer.Id
        };

        _db.Customers.Add(customer);
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();

        return Ok(BuildLoginResponse(customer));
    }

    // POST /api/auth/login — étape 1 : email + mot de passe
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == request.Email);

        // Message volontairement identique dans les deux cas : on ne révèle pas si l'email existe.
        if (customer is null || !BCrypt.Net.BCrypt.Verify(request.Password, customer.PasswordHash))
            return Unauthorized("Email ou mot de passe incorrect.");

        // 2FA activée : on ne délivre PAS encore de JWT. On génère un jeton temporaire stocké dans Redis
        // (expire seul après 5 min) que le client devra présenter avec son code à 6 chiffres.
        if (customer.TwoFactorEnabled)
        {
            var pendingToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            await _redis.StorePendingLoginAsync(pendingToken, customer.Id, PendingLoginTtl);

            return Ok(new LoginResponseDto(null, null, null, null, RequiresTwoFactor: true, PendingToken: pendingToken));
        }

        return Ok(BuildLoginResponse(customer));
    }

    // POST /api/auth/login-2fa — étape 2 : jeton temporaire + code TOTP
    [HttpPost("login-2fa")]
    public async Task<ActionResult<LoginResponseDto>> LoginTwoFactor([FromBody] LoginTwoFactorRequestDto request)
    {
        var customerId = await _redis.GetPendingLoginAsync(request.PendingToken);
        if (customerId is null)
            return BadRequest("Session expirée. Veuillez vous reconnecter.");

        var customer = await _db.Customers.FindAsync(customerId.Value);
        if (customer is null || !customer.TwoFactorEnabled || string.IsNullOrEmpty(customer.TwoFactorSecret))
            return BadRequest("Session invalide. Veuillez vous reconnecter.");

        if (!_twoFactor.VerifyCode(customer.TwoFactorSecret, request.Code))
            return Unauthorized("Code de vérification incorrect.");

        // Le jeton temporaire est à usage unique
        await _redis.RemovePendingLoginAsync(request.PendingToken);

        return Ok(BuildLoginResponse(customer));
    }

    private LoginResponseDto BuildLoginResponse(Customer customer)
    {
        var jwtKey = _config["Jwt:Key"] ?? "dev-secret-key-change-me-in-production-please";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(2);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, customer.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, customer.Email),
            new Claim("fullName", customer.FullName)
        };

        var token = new JwtSecurityToken(
            issuer: "BankApi",
            audience: "BankApiClients",
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return new LoginResponseDto(
            new JwtSecurityTokenHandler().WriteToken(token),
            customer.Id,
            customer.FullName,
            expires);
    }

    private static string GenerateFakeIban()
    {
        var random = new Random();
        var digits = string.Concat(Enumerable.Range(0, 20).Select(_ => random.Next(0, 10)));
        return $"TN{digits[..2]} {digits[2..6]} {digits[6..10]} {digits[10..14]} {digits[14..18]}";
    }
}
