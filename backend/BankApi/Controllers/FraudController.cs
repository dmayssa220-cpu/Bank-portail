using BankApi.Integrations.Fraud;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

public record FraudScoreRequestDto(decimal Amount, decimal AccountBalance);

[ApiController]
[Route("api/[controller]")]
public class FraudController : ControllerBase
{
    private readonly FraudDetectionService _fraud;

    public FraudController(FraudDetectionService fraud)
    {
        _fraud = fraud;
    }

    // POST /api/fraud/score
    // Endpoint de test manuel : score une opération hypothétique sans la créer réellement.
    [HttpPost("score")]
    public IActionResult Score([FromBody] FraudScoreRequestDto request)
    {
        var result = _fraud.Score(request.Amount, DateTime.UtcNow, request.AccountBalance);
        return Ok(result);
    }
}
