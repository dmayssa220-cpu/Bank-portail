using BankApi.Data;
using BankApi.DTOs;
using BankApi.Integrations.Fraud;
using BankApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Controllers;

[ApiController]
[Route("api/[controller]")]
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

    // POST /api/transfers
    // Squelette simplifié : à enrichir avec authentification et vérification de l'IBAN destinataire.
    // La détection de fraude est désormais branchée (voir Integrations/Fraud).
    [HttpPost]
    public async Task<IActionResult> CreateTransfer([FromBody] TransferRequestDto request)
    {
        if (request.Amount <= 0)
            return BadRequest("Le montant doit être positif.");

        var sourceAccount = await _db.Accounts.FindAsync(request.FromAccountId);
        if (sourceAccount is null)
            return NotFound("Compte source introuvable.");

        if (sourceAccount.Balance < request.Amount)
            return BadRequest("Solde insuffisant.");

        // --- Scoring de fraude AVANT le débit, sur le solde réel du compte ---
        var fraudResult = _fraud.Score(request.Amount, DateTime.UtcNow, sourceAccount.Balance);

        if (fraudResult.IsSuspicious)
        {
            _logger.LogWarning(
                "Virement suspect détecté : compte {AccountId}, montant {Amount}, probabilité {Probability:P0}, raisons : {Reasons}",
                sourceAccount.Id, request.Amount, fraudResult.Probability, string.Join(", ", fraudResult.Reasons));

            // Choix pédagogique pour ce squelette : on ne bloque PAS le virement automatiquement,
            // on le signale seulement (le champ "fraudAlert" dans la réponse permet au frontend
            // d'afficher un avertissement). Pour bloquer réellement au-delà d'un seuil, décommentez :
            // if (fraudResult.Probability > 0.85f)
            //     return StatusCode(StatusCodes.Status403Forbidden, new { message = "Virement bloqué : risque de fraude élevé.", fraudResult });
        }

        // Utilisation d'une transaction DB pour garantir l'atomicité du débit + écriture
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

        // TODO: publier un événement "TransferCreated" sur RabbitMQ/Kafka pour :
        // - notifier le client (Notification Service)
        // - synchroniser la comptabilité (Odoo Sync Service)

        return Ok(new
        {
            transactionId = transaction.Id,
            newBalance = sourceAccount.Balance,
            fraudAlert = fraudResult.IsSuspicious ? fraudResult : null
        });
    }
}
