namespace BankApi.Integrations.Messaging;

/// <summary>
/// État partagé (singleton) de la liaison avec RabbitMQ, exposé sur /health pour diagnostiquer
/// en un coup d'œil si le consommateur écoute réellement la file — sans fouiller les logs.
/// </summary>
public class RabbitMqStatus
{
    private volatile bool _consumerConnected;
    private volatile string? _lastError;

    public bool ConsumerConnected => _consumerConnected;
    public string? LastError => _lastError;
    public DateTime? LastEventProcessedUtc { get; set; }
    public long ProcessedCount;

    public void SetConnected() { _consumerConnected = true; _lastError = null; }
    public void SetDisconnected(string error) { _consumerConnected = false; _lastError = error; }
}
