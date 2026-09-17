using BankApi.Integrations.Odoo;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;

namespace BankApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OdooSyncController : ControllerBase
{
    private readonly OdooClient _odoo;
    private readonly ILogger<OdooSyncController> _logger;

    public OdooSyncController(OdooClient odoo, ILogger<OdooSyncController> logger)
    {
        _odoo = odoo;
        _logger = logger;
    }

    // GET /api/odoosync/test-connection
    // Vérifie simplement que l'authentification JSON-RPC vers Odoo fonctionne.
    [HttpGet("test-connection")]
    public async Task<IActionResult> TestConnection()
    {
        var uid = await _odoo.AuthenticateAsync();
        return Ok(new { message = "Connexion à Odoo réussie", uid });
    }

    // POST /api/odoosync/sync-customer/{customerName}/{email}
    // Démo : crée (ou retrouve) un contact (res.partner) dans Odoo CRM à partir d'un client bancaire.
    [HttpPost("sync-customer")]
    public async Task<IActionResult> SyncCustomer([FromQuery] string name, [FromQuery] string email)
    {
        // On évite les doublons : on cherche d'abord un partenaire avec cet email
        var existing = await _odoo.SearchAsync("res.partner", new JsonArray
        {
            new JsonArray { "email", "=", email }
        });

        if (existing.Length > 0)
        {
            return Ok(new { message = "Client déjà synchronisé dans Odoo", odooPartnerId = existing[0] });
        }

        var partnerId = await _odoo.CreateAsync("res.partner", new Dictionary<string, object?>
        {
            ["name"] = name,
            ["email"] = email,
            ["is_company"] = false,
            ["comment"] = "Client synchronisé automatiquement depuis Bank Portail"
        });

        _logger.LogInformation("Client {Name} synchronisé vers Odoo (id={PartnerId})", name, partnerId);

        return Ok(new { message = "Client créé dans Odoo", odooPartnerId = partnerId });
    }
}
