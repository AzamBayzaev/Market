using Market.Data;
using Market.Entity;
using Market.Implimitation.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Market.Implimitation.Services;

public class VerificationCodeService : IVerificationCodeService
{
    private readonly AppDbContext _db;
    private readonly ILogger<VerificationCodeService> _logger;

    public VerificationCodeService(AppDbContext db, ILogger<VerificationCodeService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string GenerateCode()
    {
        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        _logger.LogDebug("Verification code generated");
        return code;
    }

    public async Task SaveCodeAsync(int userId, string code, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving verification code for user. UserId: {UserId}", userId);

        var entity = new EmailVerificationCodeEntity
        {
            UserId = userId,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            IsUsed = false
        };

        _db.EmailVerificationCodeEntities.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Verification code saved. UserId: {UserId}", userId);
    }

    public async Task<bool> VerifyCodeAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Verifying email code for {Email}", email);

        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Verification failed: user not found. Email: {Email}", email);
            return false;
        }

        var record = await _db.EmailVerificationCodeEntities
            .Where(x =>
                x.UserId == user.UserId &&
                x.Code == code &&
                !x.IsUsed &&
                x.ExpiresAt > DateTime.UtcNow)
            .FirstOrDefaultAsync(cancellationToken);

        if (record == null)
        {
            _logger.LogWarning("Verification failed: code not valid or expired. Email: {Email}", email);
            return false;
        }

        record.IsUsed = true;
        user.IsVerified = true;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Email verified successfully. Email: {Email}, UserId: {UserId}", email, user.UserId);

        return true;
    }
}