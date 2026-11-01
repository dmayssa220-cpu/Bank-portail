using BankApi.Data;
using BankApi.DTOs;
using BankApi.Extensions;
using BankApi.Integrations.Notifications;
using BankApi.Integrations.TwoFactor;
using BankApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly BankDbContext _db;
    private readonly TwoFactorService _twoFactor;
    private readonly NotificationService _notifications;

    public ProfileController(BankDbContext db, TwoFactorService twoFactor, NotificationService notifications)
    {
        _db = db;
        _twoFactor = twoFactor;
        _notifications = notifications;
    }

    // GET /api/profile
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var customer = await _db.Customers.FindAsync(User.GetCustomerId());
        if (customer is null) return NotFound();

        return Ok(new ProfileDto(customer.FullName, customer.Email, customer.TwoFactorEnabled, customer.CreatedAt));
    }

    // PUT /api/profile
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest("Le nom ne peut pas être vide.");

        var customer = await _db.Customers.FindAsync(User.GetCustomerId());
        if (customer is null) return NotFound();

        customer.FullName = request.FullName.Trim();
        await _db.SaveChangesAsync();

        return Ok(new ProfileDto(customer.FullName, customer.Email, customer.TwoFactorEnabled, customer.CreatedAt));
    }

    // PUT /api/profile/password
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        var customer = await _db.Customers.FindAsync(User.GetCustomerId());
        if (customer is null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, customer.PasswordHash))
            return BadRequest("Le mot de passe actuel est incorrect.");

        if (request.NewPassword.Length < 6)
            return BadRequest("Le nouveau mot de passe doit contenir au moins 6 caractères.");

        customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _db.SaveChangesAsync();

        await _notifications.NotifyAsync(
            customer.Id,
            "Mot de passe modifié",
            "Votre mot de passe vient d'être modifié. Si ce n'est pas vous, contactez immédiatement votre banque.",
            NotificationType.Warning);

        return NoContent();
    }

    // POST /api/profile/2fa/setup — génère un secret (la 2FA n'est PAS encore active)
    [HttpPost("2fa/setup")]
    public async Task<IActionResult> SetupTwoFactor()
    {
        var customer = await _db.Customers.FindAsync(User.GetCustomerId());
        if (customer is null) return NotFound();

        if (customer.TwoFactorEnabled)
            return BadRequest("La double authentification est déjà activée.");

        customer.TwoFactorSecret = _twoFactor.GenerateSecret();
        await _db.SaveChangesAsync();

        return Ok(new TwoFactorSetupResponseDto(
            customer.TwoFactorSecret,
            _twoFactor.BuildOtpAuthUri(customer.Email, customer.TwoFactorSecret)));
    }

    // POST /api/profile/2fa/confirm — l'utilisateur prouve qu'il a bien scanné le QR code
    [HttpPost("2fa/confirm")]
    public async Task<IActionResult> ConfirmTwoFactor([FromBody] TwoFactorCodeRequestDto request)
    {
        var customer = await _db.Customers.FindAsync(User.GetCustomerId());
        if (customer is null) return NotFound();

        if (string.IsNullOrEmpty(customer.TwoFactorSecret))
            return BadRequest("Aucune configuration en cours. Relancez l'activation.");

        if (!_twoFactor.VerifyCode(customer.TwoFactorSecret, request.Code))
            return BadRequest("Code incorrect. Vérifiez l'heure de votre téléphone et réessayez.");

        customer.TwoFactorEnabled = true;
        await _db.SaveChangesAsync();

        await _notifications.NotifyAsync(
            customer.Id,
            "Double authentification activée",
            "La double authentification est maintenant active sur votre compte.",
            NotificationType.Success);

        return NoContent();
    }

    // POST /api/profile/2fa/disable — exige le mot de passe (action sensible)
    [HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor([FromBody] DisableTwoFactorRequestDto request)
    {
        var customer = await _db.Customers.FindAsync(User.GetCustomerId());
        if (customer is null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(request.Password, customer.PasswordHash))
            return BadRequest("Mot de passe incorrect.");

        customer.TwoFactorEnabled = false;
        customer.TwoFactorSecret = null;
        await _db.SaveChangesAsync();

        await _notifications.NotifyAsync(
            customer.Id,
            "Double authentification désactivée",
            "La double authentification a été désactivée sur votre compte.",
            NotificationType.Warning);

        return NoContent();
    }
}
