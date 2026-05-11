using Microsoft.Extensions.Options;
using MimeKit;
using Market.Implimitation.Interfaces;
using Market.Options;

namespace Market.Implimitation.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;
    private readonly ISmtpClientWrapper _smtp;

    public EmailService(
        IOptions<EmailSettings> settings,
        ILogger<EmailService> logger,
        ISmtpClientWrapper smtp)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _smtp = smtp ?? throw new ArgumentNullException(nameof(smtp));
    }

    public async Task SendCodeAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required", nameof(code));

        try
        {
            var message = BuildMessage(email, code);

            await _smtp.SendAsync(message, cancellationToken);

            _logger.LogInformation("Verification email sent successfully to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email to {Email}", email);
            throw;
        }
    }

    private MimeMessage BuildMessage(string email, string code)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Подтверждение регистрации";

        message.Body = new TextPart("html")
        {
            Text = BuildHtmlBody(code)
        };

        return message;
    }

    private static string BuildHtmlBody(string code)
    {
        return $@"
        <div style=""font-family: Arial, sans-serif; padding: 16px;"">
            <h2>Подтверждение регистрации</h2>
            <p>Ваш код подтверждения:</p>
            <div style=""
                font-size: 20px;
                font-weight: bold;
                padding: 10px;
                background: #f4f4f4;
                display: inline-block;
                border-radius: 6px;"">
                {code}
            </div>
            <p style=""margin-top: 16px; color: #666;"">
                Если это были не вы — просто проигнорируйте это письмо.
            </p>
        </div>";
    }
}