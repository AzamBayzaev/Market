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

public class AuthServiceIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly AuthService _service;

    private readonly Mock<IPasswordHasher<UserEntity>> _hasher;
    private readonly Mock<ITokenService> _tokenService;

    public AuthServiceIntegrationTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _hasher = new Mock<IPasswordHasher<UserEntity>>();
        _tokenService = new Mock<ITokenService>();

        _service = new AuthService(
            _db,
            _hasher.Object,
            _tokenService.Object,
            NullLogger<AuthService>.Instance
        );
    }
    

    [Fact]
    public async Task AuthenticateAsync_ShouldReturnToken_WhenCredentialsValid()
    {

        var user = new UserEntity
        {
            UserId = 1,
            Email = "test@mail.com",
            PasswordHash = "hashed_password",
            Name = "Test",
            Role = "User"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _hasher
            .Setup(x => x.VerifyHashedPassword(user, user.PasswordHash, "123"))
            .Returns(PasswordVerificationResult.Success);

        _tokenService
            .Setup(x => x.CreateToken(user))
            .Returns("jwt_token");

        var result = await _service.AuthenticateAsync(new LoginDto
        {
            Email = "test@mail.com",
            Password = "123"
        });


        Assert.True(result.Success);
        Assert.Equal("jwt_token", result.ToString());
        Assert.Null(result.Error);
    }
    

    [Fact]
    public async Task AuthenticateAsync_ShouldFail_WhenUserNotFound()
    {
        var result = await _service.AuthenticateAsync(new LoginDto
        {
            Email = "notfound@mail.com",
            Password = "123"
        });

        Assert.False(result.Success);
        Assert.Equal("InvalidCredentials", result.Error);
    }
    

    [Fact]
    public async Task AuthenticateAsync_ShouldFail_WhenPasswordInvalid()
    {
        var user = new UserEntity
        {
            UserId = 1,
            Email = "test@mail.com",
            PasswordHash = "hashed",
            Name = "Test",
            Role = "User"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _hasher
            .Setup(x => x.VerifyHashedPassword(user, user.PasswordHash, "wrong"))
            .Returns(PasswordVerificationResult.Failed);

        var result = await _service.AuthenticateAsync(new LoginDto
        {
            Email = "test@mail.com",
            Password = "wrong"
        });

        Assert.False(result.Success);
        Assert.Equal("InvalidCredentials", result.Error);
    }
    

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
