
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace BankApi.Integrations.Messaging;

/// <summary>
/// Publie des événements sur la file RabbitMQ. Enregistré en Singleton : une seule connexion
/// TCP partagée, créée au premier usage (avec quelques tentatives si RabbitMQ démarre encore).
/// </summary>
public class RabbitMqPublisher : IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly object _lock = new();
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public void Publish(BankEvent evt)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt));

        lock (_lock) // IModel n'est pas thread-safe
        {
            EnsureConnected();

            var props = _channel!.CreateBasicProperties();
            props.Persistent = true; // le message survit à un redémarrage de RabbitMQ

            _channel.BasicPublish(
                exchange: string.Empty,
                routingKey: _options.QueueName,
                basicProperties: props,
                body: body);
        }

        _logger.LogInformation("Événement publié sur RabbitMQ : {Title} (client {CustomerId})", evt.Title, evt.CustomerId);
    }

    private void EnsureConnected()
    {
        if (_connection is { IsOpen: true } && _channel is { IsOpen: true }) return;

        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password
        };

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                _connection = factory.CreateConnection("bank-api-publisher");
                _channel = _connection.CreateModel();
                _channel.QueueDeclare(_options.QueueName, durable: true, exclusive: false, autoDelete: false);
                return;
            }
            catch (Exception ex) when (attempt < 3)
            {
                _logger.LogWarning("RabbitMQ indisponible (tentative {Attempt}/3) : {Message}", attempt, ex.Message);
                Thread.Sleep(1000);
            }
        }
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}
