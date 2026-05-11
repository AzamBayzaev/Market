using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Market.Data;
using Market.Entity;
using Market.Dtos;
using Market.Implimitation.Services;
using Market.Implimitation.Interfaces;

public class RegisterServiceTests
{
    private AppDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private (RegisterService service,
             Mock<IPasswordHasher<UserEntity>> hasher,
             Mock<IVerificationCodeService> codeService,
             Mock<IEmailService> emailService,
             Mock<ILogger<RegisterService>> logger,
             AppDbContext db) CreateService()
    {
        var db = GetDbContext();
        var hasher = new Mock<IPasswordHasher<UserEntity>>();
        var codeService = new Mock<IVerificationCodeService>();
        var emailService = new Mock<IEmailService>();
        var logger = new Mock<ILogger<RegisterService>>();

        var service = new RegisterService(
            db,
            hasher.Object,
            codeService.Object,
            emailService.Object,
            logger.Object);

        return (service, hasher, codeService, emailService, logger, db);
    }

    private RegisterDto CreateDto(string name = "test", string email = "test@mail.com")
        => new RegisterDto
        {
            Name = name,
            Email = email,
            PasswordHash = "password123"
        };

    [Fact]
    public async Task RegisterAsync_ShouldReturnFalse_WhenUserAlreadyExists()
    {
        var (service, hasher, codeService, emailService, logger, db) = CreateService();

        db.Users.Add(new UserEntity
        {
            Name = "test",
            Email = "test@mail.com",
            PasswordHash = "existing-hash" 
        });

        await db.SaveChangesAsync();

        var result = await service.RegisterAsync(CreateDto());

        Assert.False(result);

        codeService.Verify(x => x.GenerateCode(), Times.Never);
        emailService.Verify(x => x.SendCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_ShouldCreateUser_AndSendCode_WhenValid()
    {
        var (service, hasher, codeService, emailService, logger, db) = CreateService();

        hasher
            .Setup(x => x.HashPassword(It.IsAny<UserEntity>(), "password123"))
            .Returns("hashed_password");

        codeService
            .Setup(x => x.GenerateCode())
            .Returns("123456");

        var result = await service.RegisterAsync(CreateDto());

        Assert.True(result);

        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == "test@mail.com");

        Assert.NotNull(user);
        Assert.Equal("hashed_password", user!.PasswordHash);

        codeService.Verify(x => x.GenerateCode(), Times.Once);
        codeService.Verify(x => x.SaveCodeAsync(user.UserId, "123456", It.IsAny<CancellationToken>()), Times.Once);

        emailService.Verify(x => x.SendCodeAsync("test@mail.com", "123456", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_ShouldCallHasher_WhenCreatingUser()
    {
        var (service, hasher, codeService, emailService, logger, db) = CreateService();

        hasher
            .Setup(x => x.HashPassword(It.IsAny<UserEntity>(), It.IsAny<string>()))
            .Returns("hashed");

        codeService.Setup(x => x.GenerateCode()).Returns("111");

        await service.RegisterAsync(CreateDto());

        hasher.Verify(x => x.HashPassword(It.IsAny<UserEntity>(), "password123"), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_ShouldNotSendEmail_WhenUserExists()
    {
        var (service, hasher, codeService, emailService, logger, db) = CreateService();

        db.Users.Add(new UserEntity
        {
            Name = "test",
            Email = "test@mail.com",
            PasswordHash = "existing-hash" 
        });

        await db.SaveChangesAsync();

        await service.RegisterAsync(CreateDto());

        emailService.Verify(
            x => x.SendCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}