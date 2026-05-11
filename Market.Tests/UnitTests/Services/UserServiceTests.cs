using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using System.Text;
using Market.Data;
using Market.Entity;
using Market.Dtos;
using Market.Implimitation.Services;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

public class UserServiceTests
{
    private AppDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private (UserService service,
             Mock<IPasswordHasher<UserEntity>> hasher,
             Mock<IDistributedCache> cache,
             Mock<ILogger<UserService>> logger,
             AppDbContext db) CreateService()
    {
        var db = GetDbContext();
        var hasher = new Mock<IPasswordHasher<UserEntity>>();
        var cache = new Mock<IDistributedCache>();
        var logger = new Mock<ILogger<UserService>>();

        var service = new UserService(db, hasher.Object, cache.Object, logger.Object);

        return (service, hasher, cache, logger, db);
    }
    
    private UserEntity CreateUser(int id = 1)
        => new UserEntity
        {
            UserId = id,
            Name = "test",
            Email = "test@mail.com",
            Role = "User",
            PasswordHash = "test-hash"
        };

    [Fact]
    public async Task CreateAsync_ShouldReturnNull_WhenInputInvalid()
    {
        var (service, hasher, cache, logger, db) = CreateService();

        var result = await service.CreateAsync(new UserCreateDto
        {
            Name = "",
            Email = "",
            Password = ""
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnNull_WhenDuplicateUser()
    {
        var (service, hasher, cache, logger, db) = CreateService();

        db.Users.Add(CreateUser());
        await db.SaveChangesAsync();

        var result = await service.CreateAsync(new UserCreateDto
        {
            Name = "test",
            Email = "test@mail.com",
            Password = "123",
            Role = "User"
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateUser_WhenValid()
    {
        var (service, hasher, cache, logger, db) = CreateService();

        hasher
            .Setup(x => x.HashPassword(It.IsAny<UserEntity>(), "123"))
            .Returns("hashed");

        var result = await service.CreateAsync(new UserCreateDto
        {
            Name = "new",
            Email = "new@mail.com",
            Password = "123",
            Role = "User"
        });

        Assert.NotNull(result);
        Assert.Equal("new", result!.Name);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnNotFound_WhenUserMissing()
    {
        var (service, hasher, cache, logger, db) = CreateService();

        var result = await service.DeleteAsync(999);

        Assert.Null(result.Item1);
        Assert.Equal("User not found", result.Item2);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteUser_WhenExists()
    {
        var (service, hasher, cache, logger, db) = CreateService();

        db.Users.Add(CreateUser());
        await db.SaveChangesAsync();

        var result = await service.DeleteAsync(1);

        Assert.NotNull(result.Item1);
        Assert.Equal("User successfully soft deleted", result.Item2);
    }

    [Fact]
    public async Task RestoreAsync_ShouldFail_WhenUserNotFound()
    {
        var (service, hasher, cache, logger, db) = CreateService();

        var result = await service.RestoreAsync(999);

        Assert.Null(result.Item1);
    }

    [Fact]
    public async Task RestoreAsync_ShouldFail_WhenNotDeleted()
    {
        var (service, hasher, cache, logger, db) = CreateService();

        db.Users.Add(CreateUser());
        await db.SaveChangesAsync();

        var result = await service.RestoreAsync(1);

        Assert.Null(result.Item1);
    }

    [Fact]
    public async Task HardDeleteAsync_ShouldReturnFalse_WhenNotFound()
    {
        var (service, hasher, cache, logger, db) = CreateService();

        var result = await service.HardDeleteAsync(999);

        Assert.False(result);
    }

    [Fact]
    public async Task HardDeleteAsync_ShouldReturnTrue_WhenDeleted()
    {
        var (service, hasher, cache, logger, db) = CreateService();

        db.Users.Add(CreateUser());
        await db.SaveChangesAsync();

        var result = await service.HardDeleteAsync(1);

        Assert.True(result);
    }
    
    [Fact]
    public async Task GetAsync_ShouldReturnFromCache_WhenCacheHit()
    {
        var (service, hasher, cache, logger, db) = CreateService();

        var cached = new List<UserDto>
        {
            new UserDto { Name = "cached", Email = "c@mail.com", Role = "User" }
        };

        var json = JsonSerializer.Serialize(cached);
        var bytes = Encoding.UTF8.GetBytes(json);

        cache
            .Setup(x => x.GetAsync(It.IsAny<string>(), default))
            .ReturnsAsync(bytes);

        var result = await service.GetAsync(new UserQueryDto
        {
            PageNumber = 1,
            SortBy = "name"
        });

        Assert.Single(result);
        Assert.Equal("cached", result.First().Name);
    }
    
    [Fact]
    public async Task GetAsync_ShouldReturnFromDb_WhenCacheMiss()
    {
        var (service, hasher, cache, logger, db) = CreateService();

        db.Users.Add(CreateUser());
        await db.SaveChangesAsync();

        cache
            .Setup(x => x.GetAsync(It.IsAny<string>(), default))
            .ReturnsAsync((byte[]?)null);

        var result = await service.GetAsync(new UserQueryDto
        {
            PageNumber = 1,
            SortBy = "name"
        });

        Assert.Single(result);
    }
}