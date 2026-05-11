using Xunit;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Market.Implimitation.Services;
using Market.Dtos;
using MimeKit;
using Market.Options;

public class EmailServiceIntegrationTests
{
    [Fact]
    public async Task SendCodeAsync_ShouldActuallySendEmail()
    {
        var settings = Options.Create(new EmailSettings
        {
            FromName = "Test App",
            FromEmail = "test@app.com"
        });

        var smtp = new FakeSmtpClientWrapper();

        var service = new EmailService(
            settings,
            NullLogger<EmailService>.Instance,
            smtp);
        
        await service.SendCodeAsync("user@mail.com", "123456");
        
        Assert.Single(smtp.SentMessages);

        var message = smtp.SentMessages.First();

        Assert.Equal("Подтверждение регистрации", message.Subject);
        Assert.Contains("user@mail.com", message.To.ToString());
        Assert.Contains("123456", ((TextPart)message.Body).Text);
    }
}