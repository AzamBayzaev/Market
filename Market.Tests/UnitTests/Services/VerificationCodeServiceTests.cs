using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Market.Data;
using Market.Entity;
using Market.Implimitation.Services;

public class VerificationCodeServiceTests
{
    private AppDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private (VerificationCodeService service,
             Mock<ILogger<VerificationCodeService>> logger,
             AppDbContext db) CreateService()
    {
        var db = GetDbContext();
        var logger = new Mock<ILogger<VerificationCodeService>>();

        var service = new VerificationCodeService(db, logger.Object);

        return (service, logger, db);
    }
    
    private UserEntity CreateUser(int id = 1) => new UserEntity
    {
        UserId = id,
        Email = "test@mail.com",
        Name = "Test",
        PasswordHash = "hash",
        IsVerified = false
    };
    

    [Fact]
    public void GenerateCode_ShouldReturn6DigitNumber()
    {
        var (service, logger, db) = CreateService();

        var code = service.GenerateCode();

        Assert.NotNull(code);
        Assert.Equal(6, code.Length);
        Assert.True(int.TryParse(code, out _));
    }
    

    [Fact]
    public async Task SaveCodeAsync_ShouldSaveCode()
    {
        var (service, logger, db) = CreateService();

        await service.SaveCodeAsync(1, "123456");

        var saved = await db.EmailVerificationCodeEntities.FirstOrDefaultAsync();

        Assert.NotNull(saved);
        Assert.Equal(1, saved!.UserId);
        Assert.Equal("123456", saved.Code);
        Assert.False(saved.IsUsed);
    }
    

    [Fact]
    public async Task VerifyCodeAsync_ShouldReturnFalse_WhenUserNotFound()
    {
        var (service, logger, db) = CreateService();

        var result = await service.VerifyCodeAsync("notfound@mail.com", "123456");

        Assert.False(result);
    }
    

    [Fact]
    public async Task VerifyCodeAsync_ShouldReturnFalse_WhenCodeInvalid()
    {
        var (service, logger, db) = CreateService();

        db.Users.Add(CreateUser());
        await db.SaveChangesAsync();

        var result = await service.VerifyCodeAsync("test@mail.com", "000000");

        Assert.False(result);
    }
    

    [Fact]
    public async Task VerifyCodeAsync_ShouldVerifyUser_WhenCodeValid()
    {
        var (service, logger, db) = CreateService();

        var user = CreateUser();
        db.Users.Add(user);

        db.EmailVerificationCodeEntities.Add(new EmailVerificationCodeEntity
        {
            UserId = 1,
            Code = "123456",
            IsUsed = false,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        });

        await db.SaveChangesAsync();

        var result = await service.VerifyCodeAsync("test@mail.com", "123456");

        Assert.True(result);

        var updatedUser = await db.Users.FirstAsync();
        Assert.True(updatedUser.IsVerified);

        var code = await db.EmailVerificationCodeEntities.FirstAsync();
        Assert.True(code.IsUsed);
    }
    

    [Fact]
    public async Task VerifyCodeAsync_ShouldReturnFalse_WhenCodeExpired()
    {
        var (service, logger, db) = CreateService();

        db.Users.Add(CreateUser());

        db.EmailVerificationCodeEntities.Add(new EmailVerificationCodeEntity
        {
            UserId = 1,
            Code = "123456",
            IsUsed = false,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        });

        await db.SaveChangesAsync();

        var result = await service.VerifyCodeAsync("test@mail.com", "123456");

        Assert.False(result);
    }
}