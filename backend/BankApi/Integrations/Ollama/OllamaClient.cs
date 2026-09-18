using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace BankApi.Integrations.Ollama;

public record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content
);

internal record OllamaChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] List<ChatMessage> Messages,
    [property: JsonPropertyName("stream")] bool Stream
);

internal record OllamaChatResponse(
    [property: JsonPropertyName("message")] ChatMessage? Message
);

/// <summary>
/// Client minimal pour l'API locale d'Ollama (https://github.com/ollama/ollama/blob/main/docs/api.md).
/// Ollama tourne dans son propre conteneur, 100% local, aucune clé API, aucun coût.
/// </summary>
public class OllamaClient
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _options;

    public OllamaClient(HttpClient http, IOptions<OllamaOptions> options)
    {
        _http = http;
        _options = options.Value;
        _http.BaseAddress = new Uri(_options.Url);
        _http.Timeout = TimeSpan.FromSeconds(60); // les modèles locaux peuvent être lents sur CPU
    }

    public async Task<string> AskAsync(List<ChatMessage> conversation, CancellationToken ct = default)
    {
        var request = new OllamaChatRequest(_options.Model, conversation, Stream: false);

        var response = await _http.PostAsJsonAsync("/api/chat", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: ct);

        return result?.Message?.Content
            ?? "Désolé, je n'ai pas pu générer de réponse (vérifiez que le modèle Ollama est bien téléchargé).";
    }
}
