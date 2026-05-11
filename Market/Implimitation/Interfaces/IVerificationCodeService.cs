namespace Market.Implimitation.Interfaces;

public interface IVerificationCodeService
{
    string GenerateCode();

    Task SaveCodeAsync(int userId, string code, CancellationToken cancellationToken = default);

    Task<bool> VerifyCodeAsync(string email, string code, CancellationToken cancellationToken = default);
}