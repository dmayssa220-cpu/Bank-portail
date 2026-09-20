using BankApi.Data;
using BankApi.DTOs;
using BankApi.Extensions;
using BankApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CardsController : ControllerBase
{
    private readonly BankDbContext _db;

    public CardsController(BankDbContext db)
    {
        _db = db;
    }

    // GET /api/cards — toutes les cartes des comptes du client connecté
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var customerId = User.GetCustomerId();

        var cards = await _db.BankCards
            .Include(c => c.Account)
            .Where(c => c.Account!.CustomerId == customerId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return Ok(cards.Select(c => ToDto(c)));
    }

    // POST /api/cards — demander une nouvelle carte pour un compte donné
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCardRequestDto request)
    {
        var customerId = User.GetCustomerId();

        var account = await _db.Accounts
            .Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.Id == request.AccountId);

        if (account is null) return NotFound("Compte introuvable.");
        if (account.CustomerId != customerId) return Forbid();

        var card = new BankCard
        {
            AccountId = account.Id,
            CardNumberMasked = GenerateMaskedCardNumber(),
            CardHolderName = string.IsNullOrWhiteSpace(request.CardHolderName)
                ? account.Customer!.FullName
                : request.CardHolderName,
            ExpiryDate = DateTime.UtcNow.AddYears(4),
            IsBlocked = false,
            DailyLimit = 2000m
        };

        _db.BankCards.Add(card);
        await _db.SaveChangesAsync();

        return Ok(ToDto(card, account.Iban));
    }

    // PATCH /api/cards/{id}/block
    [HttpPatch("{id:guid}/block")]
    public async Task<IActionResult> Block(Guid id) => await SetBlocked(id, true);

    // PATCH /api/cards/{id}/unblock
    [HttpPatch("{id:guid}/unblock")]
    public async Task<IActionResult> Unblock(Guid id) => await SetBlocked(id, false);

    // PATCH /api/cards/{id}/limit
    [HttpPatch("{id:guid}/limit")]
    public async Task<IActionResult> UpdateLimit(Guid id, [FromBody] UpdateCardLimitDto request)
    {
        if (request.DailyLimit <= 0) return BadRequest("Le plafond doit être positif.");

        var (card, forbidden) = await GetOwnedCard(id);
        if (forbidden) return Forbid();
        if (card is null) return NotFound();

        card.DailyLimit = request.DailyLimit;
        await _db.SaveChangesAsync();

        return Ok(ToDto(card));
    }

    // DELETE /api/cards/{id} — résilier une carte
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var (card, forbidden) = await GetOwnedCard(id);
        if (forbidden) return Forbid();
        if (card is null) return NotFound();

        _db.BankCards.Remove(card);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // --- Aides internes ---

    private async Task<(BankCard? card, bool forbidden)> GetOwnedCard(Guid id)
    {
        var customerId = User.GetCustomerId();

        var card = await _db.BankCards
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (card is null) return (null, false);
        if (card.Account!.CustomerId != customerId) return (null, true);

        return (card, false);
    }

    private async Task<IActionResult> SetBlocked(Guid id, bool blocked)
    {
        var (card, forbidden) = await GetOwnedCard(id);
        if (forbidden) return Forbid();
        if (card is null) return NotFound();

        card.IsBlocked = blocked;
        await _db.SaveChangesAsync();

        return Ok(ToDto(card));
    }

    private static CardDto ToDto(BankCard c, string? iban = null) => new(
        c.Id, c.CardNumberMasked, c.CardHolderName, c.ExpiryDate, c.IsBlocked, c.DailyLimit,
        iban ?? c.Account?.Iban ?? "");

    private static string GenerateMaskedCardNumber()
    {
        var random = new Random();
        var lastFour = random.Next(1000, 9999);
        return $"**** **** **** {lastFour}";
    }
}
