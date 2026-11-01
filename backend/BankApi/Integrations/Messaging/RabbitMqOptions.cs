namespace BankApi.Integrations.Messaging;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "rabbitmq";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string QueueName { get; set; } = "bank.notifications";
}

/// <summary>
/// Événement publié sur le bus lors d'une action bancaire (virement, crédit, carte...).
/// Consommé en arrière-plan par NotificationConsumer pour persister la notification et
/// envoyer un email : découplage entre l'action métier et sa notification.
/// </summary>
public record BankEvent(
    Guid CustomerId,
    string Title,
    string Message,
    string NotificationType,
    DateTime CreatedAtUtc
);
