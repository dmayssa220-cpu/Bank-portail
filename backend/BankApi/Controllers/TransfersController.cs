using BankApi.Data;
using BankApi.DTOs;
using BankApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransfersController : ControllerBase
{
    private readonly BankDbContext _db;
    private readonly ILogger<TransfersController> _logger;

    public TransfersController(BankDbContext db, ILogger<TransfersController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // POST /api/transfers
    // Squelette simplifié : à enrichir avec authentification, contrôle du solde,
    // détection de fraude (Fraud Service) et vérification de l'IBAN destinataire.
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
        // - analyser la transaction (Fraud Service)
        // - synchroniser la comptabilité (Odoo Sync Service)

        return Ok(new { transactionId = transaction.Id, newBalance = sourceAccount.Balance });
    }
}
