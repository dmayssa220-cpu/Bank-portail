using BankApi.Data;
using BankApi.Extensions;
using BankApi.Integrations.Ollama;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Controllers;

public record ChatRequestDto(string Message, List<ChatMessage>? History);

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly OllamaClient _ollama;
    private readonly BankDbContext _db;

    public ChatController(OllamaClient ollama, BankDbContext db)
    {
        _ollama = ollama;
        _db = db;
    }

    // POST /api/chat/ask
    // Démo de RAG "léger" : on injecte les données réelles du client authentifié (premier compte
    // trouvé) dans le prompt système, pour que le modèle local puisse répondre à des questions
    // du type "quel est mon solde ?" — jamais les données d'un autre client.
    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] ChatRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Le message ne peut pas être vide.");

        var customerId = User.GetCustomerId();

        var account = await _db.Accounts
            .Include(a => a.Customer)
            .Where(a => a.CustomerId == customerId)
            .FirstOrDefaultAsync();

        var recentTransactions = account is null
            ? new List<string>()
            : await _db.Transactions
                .Where(t => t.AccountId == account.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .Select(t => $"{t.CreatedAt:dd/MM/yyyy} - {t.Type} - {t.Amount} {account.Currency} - {t.Label}")
                .ToListAsync();

        var systemPrompt = $"""
            Tu es l'assistant virtuel de "Ma Banque en Ligne". Réponds en français, de façon
            claire et concise. Tu ne dois répondre qu'aux questions sur les comptes, virements
            et opérations bancaires. Si la question sort de ce cadre, réponds poliment que tu
            ne peux aider que sur les sujets bancaires.

            Voici les données du compte du client actuellement connecté :
            - Titulaire : {account?.Customer?.FullName ?? "Inconnu"}
            - IBAN : {account?.Iban ?? "Inconnu"}
            - Solde actuel : {account?.Balance.ToString("F2") ?? "0"} {account?.Currency ?? ""}
            - Dernières transactions :
            {(recentTransactions.Count > 0 ? string.Join("\n", recentTransactions) : "Aucune transaction récente")}
            """;

        var conversation = new List<ChatMessage> { new("system", systemPrompt) };

        if (request.History is not null)
            conversation.AddRange(request.History);

        conversation.Add(new ChatMessage("user", request.Message));

        var answer = await _ollama.AskAsync(conversation);

        return Ok(new { answer });
    }
}
