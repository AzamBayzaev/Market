using Market.Data;
using Market.Dtos;
using Market.Entity;
using Market.Implimitation.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Market.Implimitation.Services;

public class UserService : IUser
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<UserEntity> _hasher;
    private readonly IDistributedCache _cache;
    private readonly ILogger<UserService> _logger;

    private const string UsersVersionKey = "users:version";
    private static readonly TimeSpan UsersCacheTtl = TimeSpan.FromMinutes(5);

    public UserService(
        AppDbContext dbContext,
        IPasswordHasher<UserEntity> passwordHasher,
        IDistributedCache cache,
        ILogger<UserService> logger)
    {
        _db = dbContext;
        _hasher = passwordHasher;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<UserDto>> GetAsync(UserQueryDto query)
    {
        _logger.LogDebug("Getting users. Page: {PageNumber}, SortBy: {SortBy}", query.PageNumber, query.SortBy);

        var version = await GetUsersVersionAsync();
        var cacheKey = BuildUsersCacheKey(query, version);

        var cachedJson = await _cache.GetStringAsync(cacheKey);

        if (!string.IsNullOrWhiteSpace(cachedJson))
        {
            _logger.LogDebug("Users cache hit. Key: {CacheKey}", cacheKey);

            var cachedUsers = JsonSerializer.Deserialize<List<UserDto>>(cachedJson);
            if (cachedUsers != null)
                return cachedUsers;
        }
        else
        {
            _logger.LogDebug("Users cache miss. Key: {CacheKey}", cacheKey);
        }

        var usersQuery = _db.Users.AsNoTracking();

        usersQuery = (query.SortBy ?? "name").Trim().ToLower() switch
        {
            "email" => usersQuery.OrderBy(x => x.Email),
            "role" => usersQuery.OrderBy(x => x.Role),
            "id" => usersQuery.OrderBy(x => x.UserId),
            _ => usersQuery.OrderBy(x => x.Name)
        };

        var users = await usersQuery
            .Skip((query.PageNumber - 1) * 20)
            .Take(20)
            .Select(x => new UserDto
            {
                Name = x.Name,
                UserId = x.UserId,
                Email = x.Email,
                Role = x.Role
            })
            .ToListAsync();

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(users),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = UsersCacheTtl
            });

        _logger.LogInformation("Returned {Count} users", users.Count);

        return users;
    }

    public async Task<UserDto?> CreateAsync(UserCreateDto user)
    {
        _logger.LogInformation("Creating user. Name: {Name}, Email: {Email}, Role: {Role}", user.Name, user.Email, user.Role);

        if (string.IsNullOrWhiteSpace(user.Name) ||
            string.IsNullOrWhiteSpace(user.Email) ||
            string.IsNullOrWhiteSpace(user.Password))
        {
            _logger.LogWarning("Create user failed: invalid input");
            return null;
        }

        if (await _db.Users.AnyAsync(x => x.Email == user.Email || x.Name == user.Name))
        {
            _logger.LogWarning("Create user failed: duplicate email or name. Name: {Name}, Email: {Email}", user.Name, user.Email);
            return null;
        }

        if (user.Role != "Seller" && user.Role != "User")
        {
            _logger.LogWarning("Create user failed: invalid role. Role: {Role}", user.Role);
            return null;
        }

        var res = new UserEntity
        {
            Name = user.Name,
            Email = user.Email,
            Role = user.Role
        };

        res.PasswordHash = _hasher.HashPassword(res, user.Password);

        await _db.Users.AddAsync(res);
        await _db.SaveChangesAsync();

        await InvalidateUsersCacheAsync();

        _logger.LogInformation("User created successfully. UserId: {UserId}", res.UserId);

        return new UserDto
        {
            UserId = res.UserId,
            Name = res.Name,
            Email = res.Email,
            Role = res.Role
        };
    }

    public async Task<(UserDto?, string)> DeleteAsync(int id)
    {
        _logger.LogInformation("Deleting user. UserId: {UserId}", id);

        var res = await _db.Users.FirstOrDefaultAsync(x => x.UserId == id);

        if (res == null)
        {
            _logger.LogWarning("Delete user failed: user not found. UserId: {UserId}", id);
            return (null, "User not found");
        }

        _db.Users.Remove(res);
        await _db.SaveChangesAsync();

        await InvalidateUsersCacheAsync();

        _logger.LogInformation("User deleted successfully. UserId: {UserId}", id);

        return (new UserDto { UserId = res.UserId }, "User successfully soft deleted");
    }

    public async Task<(UserDto?, string)> RestoreAsync(int id)
    {
        _logger.LogInformation("Restoring user. UserId: {UserId}", id);

        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.UserId == id);

        if (user == null)
        {
            _logger.LogWarning("Restore user failed: user not found. UserId: {UserId}", id);
            return (null, "User not found");
        }

        if (!user.IsDeleted)
        {
            _logger.LogWarning("Restore user failed: user is not deleted. UserId: {UserId}", id);
            return (null, "User is not deleted");
        }

        var conflict = await _db.Users.AnyAsync(x =>
            (x.Email == user.Email || x.Name == user.Name) && x.UserId != user.UserId);

        if (conflict)
        {
            _logger.LogWarning("Restore user failed: conflict found. UserId: {UserId}, Email: {Email}, Name: {Name}", id, user.Email, user.Name);
            return (null, "Another active user with same email or name exists");
        }

        user.IsDeleted = false;
        user.DeletedAt = null;
        user.DeletedBy = null;

        await _db.SaveChangesAsync();
        await InvalidateUsersCacheAsync();

        _logger.LogInformation("User restored successfully. UserId: {UserId}", id);

        return (new UserDto
        {
            UserId = user.UserId,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role
        }, "User restored");
    }

    public async Task<bool> HardDeleteAsync(int id)
    {
        _logger.LogInformation("Hard deleting user. UserId: {UserId}", id);

        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.UserId == id);

        if (user == null)
        {
            _logger.LogWarning("User not found for hard delete. Id: {UserId}", id);
            return false;
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        await InvalidateUsersCacheAsync();
        return true;
    }

    private static string BuildUsersCacheKey(UserQueryDto query, string version)
    {
        var sort = string.IsNullOrWhiteSpace(query.SortBy)
            ? "name"
            : query.SortBy.Trim().ToLower();

        return $"users:v{version}:page:{query.PageNumber}:sortby:{sort}";
    }

    private async Task<string> GetUsersVersionAsync()
    {
        var version = await _cache.GetStringAsync(UsersVersionKey);

        if (string.IsNullOrWhiteSpace(version))
        {
            version = "1";
            await _cache.SetStringAsync(UsersVersionKey, version);
            _logger.LogDebug("Users version cache initialized");
        }

        return version;
    }

    private async Task InvalidateUsersCacheAsync()
    {
        var version = await _cache.GetStringAsync(UsersVersionKey);

        var nextVersion = int.TryParse(version, out var current)
            ? (current + 1).ToString()
            : "1";

        await _cache.SetStringAsync(UsersVersionKey, nextVersion);

        _logger.LogDebug("Users cache invalidated. New version: {Version}", nextVersion);
    }
}