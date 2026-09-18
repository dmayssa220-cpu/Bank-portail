using BankApi.Data;
using BankApi.DTOs;
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
    private readonly BankDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(BankDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    // POST /api/auth/register
    // Crée un client + un premier compte courant, avec mot de passe hashé (bcrypt).
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

        // On crée automatiquement un premier compte courant, avec un IBAN factice généré
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

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == request.Email);

        // On renvoie volontairement le même message d'erreur générique dans les deux cas
        // (email inconnu / mauvais mot de passe) pour ne pas révéler si un email existe.
        if (customer is null || !BCrypt.Net.BCrypt.Verify(request.Password, customer.PasswordHash))
            return Unauthorized("Email ou mot de passe incorrect.");

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

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new LoginResponseDto(tokenString, customer.Id, customer.FullName, expires);
    }

    private static string GenerateFakeIban()
    {
        var random = new Random();
        var digits = string.Concat(Enumerable.Range(0, 20).Select(_ => random.Next(0, 10)));
        return $"TN{digits[..2]} {digits[2..6]} {digits[6..10]} {digits[10..14]} {digits[14..18]}";
    }
}
