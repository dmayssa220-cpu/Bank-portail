using BankApi.Data;
using BankApi.DTOs;
using BankApi.Extensions;
using BankApi.Integrations.Fraud;
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
    private readonly ILogger<TransfersController> _logger;

    public TransfersController(BankDbContext db, FraudDetectionService fraud, ILogger<TransfersController> logger)
    {
        _db = db;
        _fraud = fraud;
        _logger = logger;
    }

    // GET /api/transfers — historique de tous les virements sortants des comptes du client connecté
    [HttpGet]
    public async Task<IActionResult> GetHistory()
    {
        var customerId = User.GetCustomerId();

        var transfers = await _db.Transactions
            .Include(t => t.Account)
            .Where(t => t.Account!.CustomerId == customerId && t.Type == TransactionType.TransferOut)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.Amount,
                t.Label,
                t.CreatedAt,
                fromAccountIban = t.Account!.Iban
            })
            .ToListAsync();

        return Ok(transfers);
    }

    // POST /api/transfers
    [HttpPost]
    public async Task<IActionResult> CreateTransfer([FromBody] TransferRequestDto request)
    {
        if (request.Amount <= 0)
            return BadRequest("Le montant doit être positif.");

        var customerId = User.GetCustomerId();

        var sourceAccount = await _db.Accounts.FindAsync(request.FromAccountId);
        if (sourceAccount is null)
            return NotFound("Compte source introuvable.");

        // Vérification cruciale : on ne peut virer que depuis SON PROPRE compte
        if (sourceAccount.CustomerId != customerId)
            return Forbid();

        if (sourceAccount.Balance < request.Amount)
            return BadRequest("Solde insuffisant.");

        // --- Scoring de fraude AVANT le débit, sur le solde réel du compte ---
        var fraudResult = _fraud.Score(request.Amount, DateTime.UtcNow, sourceAccount.Balance);

        if (fraudResult.IsSuspicious)
        {
            _logger.LogWarning(
                "Virement suspect détecté : compte {AccountId}, montant {Amount}, probabilité {Probability:P0}, raisons : {Reasons}",
                sourceAccount.Id, request.Amount, fraudResult.Probability, string.Join(", ", fraudResult.Reasons));
        }

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();

        sourceAccount.Balance -= request.Amount;

        var transaction = new Transaction
        {
            AccountId = sourceAccount.Id,
            Type = TransactionType.TransferOut,
            Amount = request.Amount,
            Label = request.Label ?? $"Virement vers {request.ToIban}"
        };

        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        _logger.LogInformation("Virement de {Amount} effectué depuis le compte {AccountId}", request.Amount, sourceAccount.Id);

        return Ok(new
        {
            transactionId = transaction.Id,
            newBalance = sourceAccount.Balance,
            fraudAlert = fraudResult.IsSuspicious ? fraudResult : null
        });
    }
}
