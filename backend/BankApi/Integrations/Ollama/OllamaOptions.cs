namespace BankApi.Integrations.Ollama;

public class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string Url { get; set; } = "http://ollama:11434";
    public string Model { get; set; } = "llama3.2:1b";
}
