using MimeKit;

namespace Market.Implimitation.Interfaces;

public interface ISmtpClientWrapper
{
    Task SendAsync(MimeMessage message, CancellationToken cancellationToken = default);
}