using BankApi.Integrations.Messaging;
using BankApi.Models;

namespace BankApi.Integrations.Notifications;

/// <summary>
/// Point d'entrée unique pour notifier un client : publie un événement sur RabbitMQ. Le
/// NotificationConsumer enregistre ensuite la notification et envoie l'email en arrière-plan.
/// La notification est "au mieux" : si RabbitMQ est indisponible, l'erreur est loggée mais
/// l'opération bancaire déjà effectuée (ex : un virement) n'est PAS annulée ni en erreur.
/// </summary>
public class NotificationService
{
    private readonly RabbitMqPublisher _publisher;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(RabbitMqPublisher publisher, ILogger<NotificationService> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public Task NotifyAsync(Guid customerId, string title, string message, NotificationType type = NotificationType.Info)
    {
        try
        {
            _publisher.Publish(new BankEvent(customerId, title, message, type.ToString(), DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Notification '{Title}' non publiée (RabbitMQ indisponible ?) — opération conservée", title);
        }

        return Task.CompletedTask;
    }
}
