using Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Identity;
using Moq;
using System.Text.Json;
using Market.Data;
using Market.Entity;
using Market.Dtos;
using Market.Implimitation.Services;

namespace Market.Tests.IntegrationTests.Services;

public class UserServiceIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly UserService _service;

    private readonly Mock<IPasswordHasher<UserEntity>> _hasher;
    private readonly IDistributedCache _cache;

    public UserServiceIntegrationTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _cache = new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(
                new MemoryDistributedCacheOptions()));

        _hasher = new Mock<IPasswordHasher<UserEntity>>();

        _service = new UserService(
            _db,
            _hasher.Object,
            _cache,
            NullLogger<UserService>.Instance
        );
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateUser()
    {
        _hasher
            .Setup(x => x.HashPassword(It.IsAny<UserEntity>(), "123"))
            .Returns("hashed");

        var result = await _service.CreateAsync(new UserCreateDto
        {
            Name = "test",
            Email = "test@mail.com",
            Password = "123",
            Role = "User"
        });

        Assert.NotNull(result);
        Assert.Equal("test", result!.Name);

        var dbUser = await _db.Users.FirstOrDefaultAsync();
        Assert.NotNull(dbUser);
        Assert.Equal("hashed", dbUser!.PasswordHash);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnNull_WhenDuplicate()
    {
        _db.Users.Add(new UserEntity
        {
            Name = "test",
            Email = "test@mail.com",
            Role = "User",
            PasswordHash = "existing_hash" // ✅ FIX
        });

        await _db.SaveChangesAsync();

        var result = await _service.CreateAsync(new UserCreateDto
        {
            Name = "test",
            Email = "test@mail.com",
            Password = "123",
            Role = "User"
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnUsers_FromDb()
    {
        _db.Users.Add(new UserEntity
        {
            Name = "A",
            Email = "a@mail.com",
            Role = "User",
            PasswordHash = "hash" // ✅ FIX
        });

        await _db.SaveChangesAsync();

        var result = await _service.GetAsync(new UserQueryDto
        {
            PageNumber = 1,
            SortBy = "name"
        });

        Assert.Single(result);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveUser()
    {
        var user = new UserEntity
        {
            Name = "test",
            Email = "test@mail.com",
            Role = "User",
            PasswordHash = "hash" // ✅ FIX
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var result = await _service.DeleteAsync(user.UserId);

        Assert.NotNull(result.Item1);
        Assert.Equal("User successfully soft deleted", result.Item2);

        var exists = await _db.Users.AnyAsync(x => x.UserId == user.UserId);
        Assert.False(exists);
    }

    [Fact]
    public async Task HardDeleteAsync_ShouldRemoveUser()
    {
        _db.Users.Add(new UserEntity
        {
            Name = "test",
            Email = "test@mail.com",
            Role = "User",
            PasswordHash = "hash" // ✅ FIX
        });

        await _db.SaveChangesAsync();

        var user = await _db.Users.FirstAsync();

        var result = await _service.HardDeleteAsync(user.UserId);

        Assert.True(result);

        var exists = await _db.Users.AnyAsync(x => x.UserId == user.UserId);
        Assert.False(exists);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnFromCache()
    {
        var cached = new List<UserDto>
        {
            new UserDto
            {
                Name = "cached",
                Email = "c@mail.com",
                Role = "User"
            }
        };

        var key = "users:v1:page:1:sortby:name";

        await _cache.SetStringAsync(key, JsonSerializer.Serialize(cached));

        var result = await _service.GetAsync(new UserQueryDto
        {
            PageNumber = 1,
            SortBy = "name"
        });

        Assert.Single(result);
        Assert.Equal("cached", result.First().Name);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}