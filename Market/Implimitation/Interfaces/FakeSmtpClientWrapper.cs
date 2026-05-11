using MimeKit;
using Market.Implimitation.Interfaces;

public class FakeSmtpClientWrapper : ISmtpClientWrapper
{
    public List<MimeMessage> SentMessages { get; } = new();

    public Task SendAsync(MimeMessage message, CancellationToken cancellationToken = default)
    {
        SentMessages.Add(message);
        return Task.CompletedTask;
    }
}