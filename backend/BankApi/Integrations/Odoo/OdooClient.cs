using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace BankApi.Integrations.Odoo;

/// <summary>
/// Client minimal pour l'API externe JSON-RPC d'Odoo.
/// Doc officielle Odoo : https://www.odoo.com/documentation/17.0/developer/reference/external_api.html
///
/// Principe :
/// 1. On s'authentifie via le service "common" -> méthode "login" -> renvoie un uid (int).
/// 2. On appelle ensuite le service "object" -> méthode "execute_kw" pour lire/créer/modifier
///    n'importe quel modèle Odoo (res.partner, account.move, crm.lead, etc.).
/// </summary>
public class OdooClient
{
    private readonly HttpClient _http;
    private readonly OdooOptions _options;
    private int? _cachedUid;

    public OdooClient(HttpClient http, IOptions<OdooOptions> options)
    {
        _http = http;
        _options = options.Value;
        _http.BaseAddress = new Uri(_options.Url);
    }

    /// <summary>Authentifie l'utilisateur Odoo et retourne son uid (mis en cache pour les appels suivants).</summary>
    public async Task<int> AuthenticateAsync(CancellationToken ct = default)
    {
        if (_cachedUid is not null) return _cachedUid.Value;

        var payload = BuildRequest("common", "login", new JsonArray
        {
            _options.Database,
            _options.Username,
            _options.Password
        });

        var result = await CallAsync(payload, ct);

        if (result is null || result.GetValueKind() != JsonValueKind.Number)
            throw new InvalidOperationException("Échec d'authentification Odoo : vérifiez ODOO_DATABASE / ODOO_USERNAME / ODOO_API_PASSWORD.");

        _cachedUid = result.GetValue<int>();
        return _cachedUid.Value;
    }

    /// <summary>
    /// Crée un enregistrement dans un modèle Odoo (ex: "res.partner") et retourne son id.
    /// </summary>
    public async Task<int> CreateAsync(string model, Dictionary<string, object?> fields, CancellationToken ct = default)
    {
        var uid = await AuthenticateAsync(ct);

        var fieldsNode = JsonSerializer.SerializeToNode(fields);

        var args = new JsonArray
        {
            _options.Database,
            uid,
            _options.Password,
            model,
            "create",
            new JsonArray { fieldsNode }
        };

        var payload = BuildRequest("object", "execute_kw", args);
        var result = await CallAsync(payload, ct);

        return result?.GetValue<int>() ?? throw new InvalidOperationException($"Échec de création sur le modèle Odoo '{model}'.");
    }

    /// <summary>
    /// Recherche des enregistrements existants (ex: retrouver un client par email avant de le recréer).
    /// </summary>
    public async Task<int[]> SearchAsync(string model, JsonArray domain, CancellationToken ct = default)
    {
        var uid = await AuthenticateAsync(ct);

        var args = new JsonArray
        {
            _options.Database,
            uid,
            _options.Password,
            model,
            "search",
            new JsonArray { domain }
        };

        var payload = BuildRequest("object", "execute_kw", args);
        var result = await CallAsync(payload, ct);

        return result?.AsArray().Select(n => n!.GetValue<int>()).ToArray() ?? Array.Empty<int>();
    }

    private static JsonObject BuildRequest(string service, string method, JsonArray args)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "call",
            ["params"] = new JsonObject
            {
                ["service"] = service,
                ["method"] = method,
                ["args"] = args
            },
            ["id"] = Random.Shared.Next(1, int.MaxValue)
        };
    }

    private async Task<JsonNode?> CallAsync(JsonObject payload, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("/jsonrpc", payload, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: ct);

        if (body?["error"] is JsonNode errorNode)
        {
            throw new InvalidOperationException($"Erreur Odoo : {errorNode.ToJsonString()}");
        }

        return body?["result"];
    }
}
