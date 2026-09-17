using BankApi.Data;
using BankApi.DTOs;
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

    // POST /api/auth/login
    // SQUELETTE UNIQUEMENT : pas de vérification de mot de passe ni de hash.
    // À remplacer par un vrai Identity Service (ASP.NET Identity + MFA) avant toute mise en production.
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == request.Email);
        if (customer is null)
            return Unauthorized("Identifiants invalides.");

        var jwtKey = _config["Jwt:Key"] ?? "dev-secret-key-change-me-in-production-please";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(15);

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

        return Ok(new LoginResponseDto(tokenString, customer.FullName, expires));
    }
}
