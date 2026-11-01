using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BankApi.Integrations.Email;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; set; } = "mailhog";
    public int Port { get; set; } = 1025;
    public string FromAddress { get; set; } = "notifications@mabanque.local";
    public string FromName { get; set; } = "Ma Banque";

    // Laissés vides avec MailHog (pas d'authentification). À renseigner si vous branchez
    // plus tard un vrai fournisseur SMTP gratuit (Brevo, Mailtrap, Resend...).
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseSsl { get; set; }
}

/// <summary>
/// Envoi réel d'emails via le protocole SMTP (MailKit). Par défaut, le serveur SMTP est
/// MailHog (conteneur local gratuit) : les emails sont vraiment envoyés via SMTP, mais captés
/// localement et consultables sur http://localhost:8025. Changer Host/Port/identifiants
/// suffit pour basculer vers un vrai fournisseur sans toucher au code.
/// </summary>
public class EmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IOptions<EmailOptions> options, ILogger<EmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string body)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();

        await client.ConnectAsync(
            _options.Host, _options.Port,
            _options.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);

        if (!string.IsNullOrEmpty(_options.Username))
            await client.AuthenticateAsync(_options.Username, _options.Password);

        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        _logger.LogInformation("Email envoyé à {Email} : {Subject}", toEmail, subject);
    }
}
