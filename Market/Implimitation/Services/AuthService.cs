using Market.Data;
using Market.Dtos;
using Market.Entity;
using Market.Implimitation.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Market.Implimitation.Services;

public sealed class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AuthService> _logger;
    private readonly IPasswordHasher<UserEntity> _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthService(
        AppDbContext db,
        IPasswordHasher<UserEntity> passwordHasher,
        ITokenService tokenService,
        ILogger<AuthService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<AuthResultDto> AuthenticateAsync(LoginDto login)
    {
        ArgumentNullException.ThrowIfNull(login);

        if (string.IsNullOrWhiteSpace(login.Email) || string.IsNullOrWhiteSpace(login.Password))
        {
            return Fail("Invalid credentials");
        }

        _logger.LogDebug("Login attempt for {Email}", login.Email);

        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Email == login.Email);

        if (user is null)
        {
            _logger.LogWarning("User not found for {Email}", login.Email);
            return Fail("Invalid credentials");
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, login.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Invalid password for {Email}", login.Email);
            return Fail("Invalid credentials");
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, login.Password);
            await _db.SaveChangesAsync();

            _logger.LogDebug("Password rehashed for {Email}", login.Email);
        }

        var token = _tokenService.CreateToken(user);

        _logger.LogInformation("User authenticated: {UserId}", user.UserId);

        return new AuthResultDto
        {
            Success = true,
            Data = new AuthDataDto
            {
                Token = token
            }
        };
    }

    private static AuthResultDto Fail(string error) =>
        new()
        {
            Success = false,
            Error = error
        };
}