using BankApi.Data;
using BankApi.DTOs;
using BankApi.Extensions;
using BankApi.Integrations.Credit;
using BankApi.Integrations.Notifications;
using BankApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BankApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CreditController : ControllerBase
{
    private readonly BankDbContext _db;
    private readonly CreditOptions _options;
    private readonly NotificationService _notifications;

    public CreditController(BankDbContext db, IOptions<CreditOptions> options, NotificationService notifications)
    {
        _db = db;
        _options = options.Value;
        _notifications = notifications;
    }

    // POST /api/credit/simulate — simulation pure, ne crée rien en base
    [HttpPost("simulate")]
    public IActionResult Simulate([FromBody] CreditSimulationRequestDto request)
    {
        var validationError = Validate(request.Amount, request.DurationMonths);
        if (validationError is not null) return BadRequest(validationError);

        var result = CreditCalculator.Simulate(request.Amount, request.DurationMonths, _options.AnnualRatePercent);

        return Ok(new CreditSimulationResponseDto(
            request.Amount, request.DurationMonths, result.AnnualRatePercent,
            result.MonthlyPayment, result.TotalCost, result.TotalInterest));
    }

    // POST /api/credit/apply — enregistre une vraie demande de crédit (statut "Pending")
    [HttpPost("apply")]
    public async Task<ActionResult<CreditRequestResponseDto>> Apply([FromBody] CreditSimulationRequestDto request)
    {
        var validationError = Validate(request.Amount, request.DurationMonths);
        if (validationError is not null) return BadRequest(validationError);

        var customerId = User.GetCustomerId();
        var result = CreditCalculator.Simulate(request.Amount, request.DurationMonths, _options.AnnualRatePercent);

        var creditRequest = new CreditRequest
        {
            CustomerId = customerId,
            Amount = request.Amount,
            DurationMonths = request.DurationMonths,
            AnnualRate = result.AnnualRatePercent,
            MonthlyPayment = result.MonthlyPayment,
            TotalCost = result.TotalCost,
            Status = CreditRequestStatus.Pending
        };

        _db.CreditRequests.Add(creditRequest);
        await _db.SaveChangesAsync();

        await _notifications.NotifyAsync(
            customerId,
            "Demande de crédit reçue",
            $"Votre demande de {request.Amount} sur {request.DurationMonths} mois a été enregistrée et est en attente d'examen.",
            NotificationType.Info);

        return Ok(ToDto(creditRequest));
    }

    // GET /api/credit/my-requests — historique des demandes du client connecté
    [HttpGet("my-requests")]
    public async Task<IActionResult> MyRequests()
    {
        var customerId = User.GetCustomerId();

        var requests = await _db.CreditRequests
            .Where(c => c.CustomerId == customerId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return Ok(requests.Select(ToDto));
    }

    private string? Validate(decimal amount, int durationMonths)
    {
        if (amount < _options.MinAmount || amount > _options.MaxAmount)
            return $"Le montant doit être compris entre {_options.MinAmount} et {_options.MaxAmount}.";

        if (durationMonths < _options.MinDurationMonths || durationMonths > _options.MaxDurationMonths)
            return $"La durée doit être comprise entre {_options.MinDurationMonths} et {_options.MaxDurationMonths} mois.";

        return null;
    }

    private static CreditRequestResponseDto ToDto(CreditRequest c) => new(
        c.Id, c.Amount, c.DurationMonths, c.MonthlyPayment, c.TotalCost, c.Status.ToString(), c.CreatedAt);
}
