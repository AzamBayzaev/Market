using Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Market.Data;
using Market.Entity;
using Market.Implimitation.Services;

namespace Market.Tests.IntegrationTests.Services;

public class VerificationCodeServiceIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly VerificationCodeService _service;

    public VerificationCodeServiceIntegrationTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _service = new VerificationCodeService(
            _db,
            NullLogger<VerificationCodeService>.Instance);
    }

   
    private async Task<UserEntity> CreateUserAsync()
    {
        var user = new UserEntity
        {
            UserId = 1,
            Email = "test@mail.com",
            Name = "Test User",              
            PasswordHash = "hashed_password", 
            IsVerified = false
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return user;
    }

    [Fact]
    public void GenerateCode_ShouldReturn6Digits()
    {
        var code = _service.GenerateCode();

        Assert.NotNull(code);
        Assert.Equal(6, code.Length);
        Assert.True(int.TryParse(code, out _));
    }

    [Fact]
    public async Task SaveCodeAsync_ShouldSaveCodeInDatabase()
    {
        await CreateUserAsync();

        await _service.SaveCodeAsync(1, "123456");

        var saved = await _db.EmailVerificationCodeEntities.FirstOrDefaultAsync();

        Assert.NotNull(saved);
        Assert.Equal(1, saved!.UserId);
        Assert.Equal("123456", saved.Code);
        Assert.False(saved.IsUsed);
        Assert.True(saved.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task VerifyCodeAsync_ShouldVerifyUser_WhenCodeIsValid()
    {
        await CreateUserAsync();

        _db.EmailVerificationCodeEntities.Add(new EmailVerificationCodeEntity
        {
            UserId = 1,
            Code = "123456",
            IsUsed = false,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        });

        await _db.SaveChangesAsync();

        var result = await _service.VerifyCodeAsync("test@mail.com", "123456");

        Assert.True(result);

        var user = await _db.Users.FirstAsync();
        Assert.True(user.IsVerified);

        var code = await _db.EmailVerificationCodeEntities.FirstAsync();
        Assert.True(code.IsUsed);
    }

    [Fact]
    public async Task VerifyCodeAsync_ShouldReturnFalse_WhenUserNotFound()
    {
        var result = await _service.VerifyCodeAsync("notfound@mail.com", "123456");

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyCodeAsync_ShouldReturnFalse_WhenCodeInvalid()
    {
        await CreateUserAsync();

        var result = await _service.VerifyCodeAsync("test@mail.com", "000000");

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyCodeAsync_ShouldReturnFalse_WhenCodeExpired()
    {
        await CreateUserAsync();

        _db.EmailVerificationCodeEntities.Add(new EmailVerificationCodeEntity
        {
            UserId = 1,
            Code = "123456",
            IsUsed = false,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10)
        });

        await _db.SaveChangesAsync();

        var result = await _service.VerifyCodeAsync("test@mail.com", "123456");

        Assert.False(result);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
