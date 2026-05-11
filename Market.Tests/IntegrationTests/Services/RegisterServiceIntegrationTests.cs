using Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Microsoft.AspNetCore.Identity;
using Market.Data;
using Market.Entity;
using Market.Dtos;
using Market.Implimitation.Services;
using Market.Implimitation.Interfaces;

namespace Market.Tests.IntegrationTests.Services;

public class RegisterServiceIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly RegisterService _service;

    private readonly Mock<IPasswordHasher<UserEntity>> _hasher;
    private readonly Mock<IVerificationCodeService> _codeService;
    private readonly Mock<IEmailService> _emailService;

    public RegisterServiceIntegrationTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _hasher = new Mock<IPasswordHasher<UserEntity>>();
        _codeService = new Mock<IVerificationCodeService>();
        _emailService = new Mock<IEmailService>();

        _service = new RegisterService(
            _db,
            _hasher.Object,
            _codeService.Object,
            _emailService.Object,
            NullLogger<RegisterService>.Instance
        );
    }


    private UserEntity CreateUser(string email = "test@mail.com")
    {
        return new UserEntity
        {
            Name = "test",
            Email = email,
            PasswordHash = "existing_hash", 
            Role = "User"
        };
    }

    [Fact]
    public async Task RegisterAsync_ShouldCreateUser_AndSendCode()
    {
        _hasher
            .Setup(x => x.HashPassword(It.IsAny<UserEntity>(), "123"))
            .Returns("hashed_password");

        _codeService
            .Setup(x => x.GenerateCode())
            .Returns("111111");

        var dto = new RegisterDto
        {
            Name = "test",
            Email = "test@mail.com",
            PasswordHash = "123"
        };

        var result = await _service.RegisterAsync(dto);

        Assert.True(result);

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == "test@mail.com");

        Assert.NotNull(user);
        Assert.Equal("hashed_password", user!.PasswordHash);

        _codeService.Verify(x => x.GenerateCode(), Times.Once);

        _codeService.Verify(x =>
            x.SaveCodeAsync(user.UserId, "111111", It.IsAny<CancellationToken>()),
            Times.Once);

        _emailService.Verify(x =>
            x.SendCodeAsync("test@mail.com", "111111", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_ShouldReturnFalse_WhenUserAlreadyExists()
    {
        _db.Users.Add(CreateUser()); 
        await _db.SaveChangesAsync();

        var dto = new RegisterDto
        {
            Name = "test",
            Email = "test@mail.com",
            PasswordHash = "123"
        };

        var result = await _service.RegisterAsync(dto);

        Assert.False(result);

        _codeService.Verify(x => x.GenerateCode(), Times.Never);

        _emailService.Verify(x =>
            x.SendCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}