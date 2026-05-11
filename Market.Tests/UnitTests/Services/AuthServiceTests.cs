using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Market.Data;
using Market.Entity;
using Market.Implimitation.Services;
using Market.Implimitation.Interfaces;


public class AuthServiceTests
{
    private AppDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private (AuthService service,
             Mock<IPasswordHasher<UserEntity>> hasher,
             Mock<ITokenService> tokenService,
             Mock<ILogger<AuthService>> logger,
             AppDbContext db) CreateService()
    {
        var db = GetDbContext();
        var hasher = new Mock<IPasswordHasher<UserEntity>>();
        var tokenService = new Mock<ITokenService>();
        var logger = new Mock<ILogger<AuthService>>();

        var service = new AuthService(db, hasher.Object, tokenService.Object, logger.Object);

        return (service, hasher, tokenService, logger, db);
    }

    private UserEntity CreateUser(
        string name = "test",
        string email = "test@mail.com",
        string passwordHash = "hash")
        => new UserEntity
        {
            Name = name,
            Email = email,
            PasswordHash = passwordHash,
            Role = "User"
        };

    [Fact]
    public async Task AuthenticateAsync_UserNotFound_ReturnsInvalidCredentials()
    {
        var (service, _, tokenService, logger, _) = CreateService();

        var result = await service.AuthenticateAsync(new LoginDto
        {
            Email = "notfound@mail.com",
            Password = "123"
        });

        Assert.False(result.Success);
        Assert.Equal("InvalidCredentials", result.Error);

        tokenService.Verify(x => x.CreateToken(It.IsAny<UserEntity>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_WrongPassword_ReturnsInvalidCredentials()
    {
        var (service, hasher, tokenService, logger, db) = CreateService();

        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        hasher
            .Setup(x => x.VerifyHashedPassword(It.IsAny<UserEntity>(), user.PasswordHash, "123"))
            .Returns(PasswordVerificationResult.Failed);

        var result = await service.AuthenticateAsync(new LoginDto
        {
            Email = user.Email,
            Password = "123"
        });

        Assert.False(result.Success);
        Assert.Equal("InvalidCredentials", result.Error);

        tokenService.Verify(x => x.CreateToken(It.IsAny<UserEntity>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_Success_ReturnsToken()
    {
        var (service, hasher, tokenService, logger, db) = CreateService();

        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        hasher
            .Setup(x => x.VerifyHashedPassword(It.IsAny<UserEntity>(), user.PasswordHash, "123"))
            .Returns(PasswordVerificationResult.Success);

        tokenService
            .Setup(x => x.CreateToken(It.IsAny<UserEntity>()))
            .Returns("fake-jwt");

        var result = await service.AuthenticateAsync(new LoginDto
        {
            Email = user.Email,
            Password = "123"
        });

        Assert.True(result.Success);
        Assert.Equal("fake-jwt", result.Data!.Token);

        tokenService.Verify(x => x.CreateToken(It.IsAny<UserEntity>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_RehashNeeded_UpdatesPassword_AndReturnsToken()
    {
        var (service, hasher, tokenService, logger, db) = CreateService();

        var user = CreateUser(passwordHash: "old-hash");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        hasher
            .Setup(x => x.VerifyHashedPassword(It.IsAny<UserEntity>(), "old-hash", "123"))
            .Returns(PasswordVerificationResult.SuccessRehashNeeded);

        hasher
            .Setup(x => x.HashPassword(It.IsAny<UserEntity>(), "123"))
            .Returns("new-hash");

        tokenService
            .Setup(x => x.CreateToken(It.IsAny<UserEntity>()))
            .Returns("token");

        var result = await service.AuthenticateAsync(new LoginDto
        {
            Email = user.Email,
            Password = "123"
        });

        var updatedUser = await db.Users.FirstOrDefaultAsync(x => x.Email == user.Email);

        Assert.True(result.Success);
        Assert.Equal("new-hash", updatedUser!.PasswordHash);
        Assert.Equal("fake-jwt", result.Data!.Token);

        tokenService.Verify(x => x.CreateToken(It.IsAny<UserEntity>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_Exception_ReturnsServerError()
    {
        var (service, hasher, tokenService, logger, db) = CreateService();

        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        hasher
            .Setup(x => x.VerifyHashedPassword(It.IsAny<UserEntity>(), It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new Exception("boom"));

        var result = await service.AuthenticateAsync(new LoginDto
        {
            Email = user.Email,
            Password = "123"
        });

        Assert.False(result.Success);
        Assert.Equal("ServerError", result.Error);

        tokenService.Verify(x => x.CreateToken(It.IsAny<UserEntity>()), Times.Never);
    }
}