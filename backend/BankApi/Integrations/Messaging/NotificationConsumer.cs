using System.Text;
using System.Text.Json;
using BankApi.Data;
using BankApi.Integrations.Email;
using BankApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BankApi.Integrations.Messaging;

/// <summary>
/// Service d'arrière-plan qui écoute la file RabbitMQ. Pour chaque événement reçu :
///  1) enregistre la notification en base (visible dans la cloche du frontend) ;
///  2) envoie un email via SMTP (3 tentatives ; un échec d'email n'annule pas la notification).
/// Il se reconnecte tout seul si RabbitMQ n'est pas prêt au démarrage ou si la connexion tombe.
/// </summary>
public class NotificationConsumer : BackgroundService
{
    private const int EmailAttempts = 3;

    private readonly RabbitMqOptions _options;
    private readonly RabbitMqStatus _status;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationConsumer> _logger;

    public NotificationConsumer(
        IOptions<RabbitMqOptions> options,
        RabbitMqStatus status,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationConsumer> logger)
    {
        _options = options.Value;
        _status = status;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Rend la main immédiatement : le démarrage de l'API n'attend pas RabbitMQ.
        await Task.Yield();

        _logger.LogInformation(
            "NotificationConsumer démarré — connexion à RabbitMQ {Host}:{Port} (utilisateur '{User}', file '{Queue}')",
            _options.Host, _options.Port, _options.Username, _options.QueueName);

        // Boucle sans fin : on ne laisse JAMAIS ce service planter l'application.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunUntilDisconnectedAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _status.SetDisconnected($"{ex.GetType().Name}: {ex.Message}");
                _logger.LogWarning(
                    "Consumer : connexion à RabbitMQ impossible ({Type}: {Message}). Nouvelle tentative dans 5 s…",
                    ex.GetType().Name, ex.Message);
            }

            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunUntilDisconnectedAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = false // c'est notre boucle qui gère la reconnexion
        };

        using var connection = factory.CreateConnection("bank-api-consumer");
        using var channel = connection.CreateModel();

        channel.QueueDeclare(_options.QueueName, durable: true, exclusive: false, autoDelete: false);
        channel.BasicQos(prefetchSize: 0, prefetchCount: 5, global: false);

        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.ConnectionShutdown += (_, args) => lost.TrySetResult();

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var evt = JsonSerializer.Deserialize<BankEvent>(json);

                if (evt is not null)
                    await HandleEventAsync(evt);

                channel.BasicAck(ea.DeliveryTag, multiple: false);
                Interlocked.Increment(ref _status.ProcessedCount);
                _status.LastEventProcessedUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                // Échec réel (ex : base de données indisponible) : on remet en file, mais après une
                // pause, pour ne pas boucler à pleine vitesse sur le même message.
                _logger.LogError(ex, "Échec du traitement d'un événement, remise en file dans 5 s");
                await Task.Delay(TimeSpan.FromSeconds(5));
                channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        channel.BasicConsume(_options.QueueName, autoAck: false, consumer);

        _status.SetConnected();
        _logger.LogInformation("Consumer RabbitMQ démarré sur la file {Queue}", _options.QueueName);

        // On reste ici tant que la connexion vit ; si elle tombe, on sort et la boucle reconnecte.
        using (stoppingToken.Register(() => lost.TrySetResult()))
        {
            await lost.Task;
        }

        if (!stoppingToken.IsCancellationRequested)
        {
            _status.SetDisconnected("Connexion à RabbitMQ perdue");
            _logger.LogWarning("Consumer : connexion RabbitMQ perdue, reconnexion…");
        }
    }

    private async Task HandleEventAsync(BankEvent evt)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<EmailSender>();

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == evt.CustomerId);
        if (customer is null)
        {
            _logger.LogWarning("Événement ignoré : client {CustomerId} introuvable", evt.CustomerId);
            return; // message acquitté : le rejouer n'y changerait rien
        }

        Enum.TryParse<NotificationType>(evt.NotificationType, out var type);

        db.Notifications.Add(new Notification
        {
            CustomerId = evt.CustomerId,
            Title = evt.Title,
            Message = evt.Message,
            Type = type,
            CreatedAt = evt.CreatedAtUtc
        });
        await db.SaveChangesAsync();

        // L'email est un "bonus" : s'il échoue, la notification est déjà enregistrée. On réessaie
        // quelques fois puis on abandonne en le loggant (sans rejouer tout l'événement, ce qui
        // dupliquerait la notification dans la cloche).
        for (var attempt = 1; attempt <= EmailAttempts; attempt++)
        {
            try
            {
                await emailSender.SendAsync(
                    customer.Email,
                    customer.FullName,
                    $"[Ma Banque] {evt.Title}",
                    $"Bonjour {customer.FullName},\n\n{evt.Message}\n\nCordialement,\nVotre banque en ligne");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Envoi d'email échoué (tentative {Attempt}/{Max}) : {Message}",
                    attempt, EmailAttempts, ex.Message);

                if (attempt < EmailAttempts)
                    await Task.Delay(TimeSpan.FromSeconds(2));
                else
                    _logger.LogError("Email abandonné pour {Email} : {Title}", customer.Email, evt.Title);
            }
        }
    }
}
