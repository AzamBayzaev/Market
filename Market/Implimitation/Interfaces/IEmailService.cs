namespace Market.Implimitation.Interfaces;

public interface IEmailService
{
    Task SendCodeAsync(string email, string code, CancellationToken cancellationToken = default);
}