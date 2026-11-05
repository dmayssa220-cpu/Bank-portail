using BankApi.Data;
using BankApi.DTOs;
using BankApi.Extensions;
using BankApi.Integrations.Caching;
using BankApi.Integrations.Fraud;
using BankApi.Integrations.Notifications;
using BankApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransfersController : ControllerBase
{
    private readonly BankDbContext _db;
    private readonly FraudDetectionService _fraud;
    private readonly NotificationService _notifications;
    private readonly RedisCacheService _cache;
    private readonly ILogger<TransfersController> _logger;

    public TransfersController(
        BankDbContext db,
        FraudDetectionService fraud,
        NotificationService notifications,
        RedisCacheService cache,
        ILogger<TransfersController> logger)
    {
        _db = db;
        _fraud = fraud;
        _notifications = notifications;
        _cache = cache;
        _logger = logger;
    }

    // GET /api/transfers — virements émis ET reçus des comptes du client connecté
    [HttpGet]
    public async Task<IActionResult> GetHistory()
    {
        var customerId = User.GetCustomerId();

        var rows = await _db.Transactions
            .Where(t => t.Account!.CustomerId == customerId
                        && (t.Type == TransactionType.TransferOut || t.Type == TransactionType.TransferIn))
            .OrderByDescending(t => t.CreatedAt)
            .Take(100)
            .Select(t => new { t.Id, t.Type, t.Amount, t.Label, t.CreatedAt, AccountIban = t.Account!.Iban })
            .ToListAsync();

        return Ok(rows.Select(r => new
        {
            r.Id,
            Type = r.Type.ToString(),
            r.Amount,
            r.Label,
            r.CreatedAt,
            r.AccountIban
        }));
    }

    // POST /api/transfers
    // En-tête optionnel "Idempotency-Key" : rejouer la même requête (double clic, nouvel essai après
    // une coupure réseau) renvoie le résultat d'origine au lieu de débiter une seconde fois.
    [HttpPost]
    public async Task<IActionResult> CreateTransfer(
        [FromBody] TransferRequestDto request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        if (request.Amount <= 0)
            return BadRequest("Le montant doit être positif.");

        if (decimal.Round(request.Amount, 2) != request.Amount)
            return BadRequest("Le montant ne peut pas avoir plus de 2 décimales.");

        if (string.IsNullOrWhiteSpace(request.ToIban))
            return BadRequest("L'IBAN du bénéficiaire est obligatoire.");

        var customerId = User.GetCustomerId();

        // --- Idempotence : la clé est préfixée par l'id du client (deux clients ne peuvent pas se télescoper) ---
        var keyBase = string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString("N") : idempotencyKey.Trim();
        if (keyBase.Length > 100)
            return BadRequest("Clé d'idempotence trop longue (100 caractères maximum).");

        var outKey = $"{customerId:N}:{keyBase}";
        var inKey = $"{outKey}:in";

        var previous = await _db.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.IdempotencyKey == outKey);
        if (previous is not null)
        {
            var currentBalance = await _db.Accounts.AsNoTracking()
                .Where(a => a.Id == previous.AccountId).Select(a => a.Balance).FirstAsync();
            var wasInternal = await _db.Transactions.AsNoTracking().AnyAsync(t => t.IdempotencyKey == inKey);

            return Ok(new TransferResultDto(previous.Id, currentBalance, wasInternal, null, Replayed: true, null));
        }

        // --- Compte source : doit appartenir au client connecté ---
        var source = await _db.Accounts.AsNoTracking()
            .Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.Id == request.FromAccountId);

        if (source is null) return NotFound("Compte source introuvable.");
        if (source.CustomerId != customerId) return Forbid();

        // --- Compte destinataire : interne à la banque si l'IBAN existe, sinon virement externe ---
        var normalizedIban = new string(request.ToIban.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

        var destination = await _db.Accounts.AsNoTracking()
            .Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.Iban.Replace(" ", "").ToUpper() == normalizedIban);

        if (destination is not null)
        {
            if (destination.Id == source.Id)
                return BadRequest("Le compte source et le compte bénéficiaire sont identiques.");

            if (destination.Currency != source.Currency)
                return BadRequest("Les virements entre devises différentes ne sont pas encore pris en charge.");
        }

        if (source.Balance < request.Amount)
            return BadRequest("Solde insuffisant.");

        // --- Détection de fraude AVANT le débit, sur le solde réel ---
        var fraudResult = _fraud.Score(request.Amount, DateTime.UtcNow, source.Balance);

        if (fraudResult.IsSuspicious)
        {
            _logger.LogWarning(
                "Virement suspect détecté : compte {AccountId}, montant {Amount}, probabilité {Probability:P0}, raisons : {Reasons}",
                source.Id, request.Amount, fraudResult.Probability, string.Join(", ", fraudResult.Reasons));
        }

        // --- Débit + crédit atomiques ---
        // Le débit est une mise à jour SQL conditionnelle (WHERE solde >= montant) : même avec deux virements
        // simultanés sur le même compte, le solde ne peut jamais devenir négatif. Les deux comptes sont
        // verrouillés toujours dans le même ordre (par id) pour éviter les interblocages (A→B et B→A en même temps).
        await using var dbTransaction = await _db.Database.BeginTransactionAsync();

        bool debited;
        if (destination is null)
        {
            debited = await DebitAsync(source.Id, request.Amount);
        }
        else if (source.Id.CompareTo(destination.Id) < 0)
        {
            debited = await DebitAsync(source.Id, request.Amount);
            if (debited) await CreditAsync(destination.Id, request.Amount);
        }
        else
        {
            await CreditAsync(destination.Id, request.Amount);
            debited = await DebitAsync(source.Id, request.Amount);
        }

        if (!debited)
            return BadRequest("Solde insuffisant."); // la transaction n'est pas validée : tout est annulé

        var outTransaction = new Transaction
        {
            AccountId = source.Id,
            Type = TransactionType.TransferOut,
            Amount = request.Amount,
            Label = request.Label ?? $"Virement vers {request.ToIban}",
            IdempotencyKey = outKey
        };
        _db.Transactions.Add(outTransaction);

        if (destination is not null)
        {
            _db.Transactions.Add(new Transaction
            {
                AccountId = destination.Id,
                Type = TransactionType.TransferIn,
                Amount = request.Amount,
                Label = $"Virement reçu de {MaskName(source.Customer!.FullName)}",
                IdempotencyKey = inKey
            });
        }

        try
        {
            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();
        }
        catch (DbUpdateException ex)
        {
            // Cas typique : deux requêtes identiques (même clé d'idempotence) arrivées en même temps
            _logger.LogWarning(ex, "Virement rejeté à l'enregistrement (clé d'idempotence déjà utilisée ?)");
            return Conflict("Ce virement est déjà en cours de traitement.");
        }

        _logger.LogInformation("Virement de {Amount} effectué : {From} → {To}",
            request.Amount, source.Id, destination?.Id.ToString() ?? "externe");

        // --- Après validation : caches, notifications ---
        var newBalance = await _db.Accounts.AsNoTracking()
            .Where(a => a.Id == source.Id).Select(a => a.Balance).FirstAsync();

        await _cache.RemoveAsync(AccountsController.CacheKey(customerId));
        if (destination is not null)
            await _cache.RemoveAsync(AccountsController.CacheKey(destination.CustomerId));

        await _notifications.NotifyAsync(
            customerId,
            fraudResult.IsSuspicious ? "Virement signalé" : "Virement effectué",
            fraudResult.IsSuspicious
                ? $"Votre virement de {request.Amount} {source.Currency} vers {request.ToIban} a été effectué mais présente un profil de risque élevé. Vérifiez qu'il s'agit bien de vous."
                : $"Un virement de {request.Amount} {source.Currency} vers {request.ToIban} a été effectué avec succès.",
            fraudResult.IsSuspicious ? NotificationType.Warning : NotificationType.Success);

        if (destination is not null && destination.CustomerId != customerId)
        {
            await _notifications.NotifyAsync(
                destination.CustomerId,
                "Virement reçu",
                $"Vous avez reçu {request.Amount} {destination.Currency} de {MaskName(source.Customer!.FullName)} sur le compte {destination.Iban}.",
                NotificationType.Success);
        }

        return Ok(new TransferResultDto(
            outTransaction.Id,
            newBalance,
            IsInternal: destination is not null,
            BeneficiaryName: destination is null ? null : MaskName(destination.Customer!.FullName),
            Replayed: false,
            FraudAlert: fraudResult.IsSuspicious ? fraudResult : null));
    }

    private async Task<bool> DebitAsync(Guid accountId, decimal amount)
    {
        var rows = await _db.Accounts
            .Where(a => a.Id == accountId && a.Balance >= amount)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Balance, a => a.Balance - amount));

        return rows == 1;
    }

    private Task<int> CreditAsync(Guid accountId, decimal amount) =>
        _db.Accounts
            .Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Balance, a => a.Balance + amount));

    // "Ahmed Ben Salah" -> "Ahmed S." : on ne révèle pas l'identité complète d'un tiers
    private static string MaskName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length <= 1 ? fullName : $"{parts[0]} {parts[^1][0]}.";
    }
}
