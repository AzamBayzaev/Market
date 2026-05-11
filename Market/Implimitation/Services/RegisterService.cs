using Market.Data;
using Market.Dtos;
using Market.Entity;
using Market.Implimitation.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Market.Implimitation.Services;

public class RegisterService : IRegisterService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<UserEntity> _hasher;
    private readonly IVerificationCodeService _codeService;
    private readonly IEmailService _emailService;
    private readonly ILogger<RegisterService> _logger;

    public RegisterService(
        AppDbContext db,
        IPasswordHasher<UserEntity> hasher,
        IVerificationCodeService codeService,
        IEmailService emailService,
        ILogger<RegisterService> logger)
    {
        _db = db;
        _hasher = hasher;
        _codeService = codeService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<bool> RegisterAsync(RegisterDto dto, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Register attempt. Name: {Name}, Email: {Email}", dto.Name, dto.Email);

        var exist = await _db.Users.AnyAsync(
            x => x.Email == dto.Email || x.Name == dto.Name,
            cancellationToken);

        if (exist)
        {
            _logger.LogWarning("Register failed: user already exists. Name: {Name}, Email: {Email}", dto.Name, dto.Email);
            return false;
        }

        var newuser = new UserEntity
        {
            Name = dto.Name,
            Email = dto.Email,
            IsVerified = false
        };

        newuser.PasswordHash = _hasher.HashPassword(newuser, dto.PasswordHash);

        await _db.Users.AddAsync(newuser, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User created. UserId: {UserId}, Email: {Email}", newuser.UserId, newuser.Email);

        var code = _codeService.GenerateCode();

        await _codeService.SaveCodeAsync(newuser.UserId, code, cancellationToken);
        await _emailService.SendCodeAsync(newuser.Email, code, cancellationToken);

        _logger.LogInformation("Verification code generated and sent. UserId: {UserId}, Email: {Email}", newuser.UserId, newuser.Email);

        return true;
    }
}