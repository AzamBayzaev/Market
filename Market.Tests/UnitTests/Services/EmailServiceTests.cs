using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MimeKit;
using Market.Implimitation.Interfaces;
using Market.Implimitation.Services;
using Xunit;
using Market.Options;
namespace Market.Tests.UnitTests.Services;
using Microsoft.Extensions.Options;

public class EmailServiceTests
{
    [Fact]
    public async Task SendCodeAsync_Should_Send_Email_Successfully()
    {
        var settings = Options.Create(new EmailSettings
        {
            FromName = "Test",
            FromEmail = "test@mail.com",
            UserName = "user",
            Password = "pass"
        });

        var loggerMock = new Mock<ILogger<EmailService>>();
        var smtpMock = new Mock<ISmtpClientWrapper>();

        smtpMock
            .Setup(x => x.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new EmailService(
            settings,
            loggerMock.Object,
            smtpMock.Object);

        await service.SendCodeAsync("test@mail.com", "123456");

        smtpMock.Verify(x => x.SendAsync(
            It.Is<MimeMessage>(m =>
                m.Subject == "Подтверждение регистрации" &&
                m.To.Mailboxes.Any(t => t.Address == "test@mail.com") &&
                (m.HtmlBody ?? string.Empty).Contains("123456")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}