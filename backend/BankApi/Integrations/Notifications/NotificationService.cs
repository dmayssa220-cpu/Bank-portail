using BankApi.Data;
using BankApi.Models;

namespace BankApi.Integrations.Notifications;

/// <summary>
/// Service de notifications. Crée une notification "in-app" (persistée en base, visible
/// dans la cloche de notification du frontend) et simule en parallèle l'envoi d'un email
/// (simplement loggé dans la console — brancher un vrai fournisseur SMTP gratuit comme
/// Mailtrap ou Resend en mode test serait la suite logique, hors périmètre de ce squelette).
/// </summary>
public class NotificationService
{
    private readonly BankDbContext _db;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(BankDbContext db, ILogger<NotificationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task NotifyAsync(Guid customerId, string title, string message, NotificationType type = NotificationType.Info)
    {
        var notification = new Notification
        {
            CustomerId = customerId,
            Title = title,
            Message = message,
            Type = type
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        // --- Simulation d'envoi email (gratuit, aucun SMTP réel configuré) ---
        _logger.LogInformation(
            "[EMAIL SIMULÉ] Destinataire: client {CustomerId} | Sujet: {Title} | Message: {Message}",
            customerId, title, message);
    }
}
