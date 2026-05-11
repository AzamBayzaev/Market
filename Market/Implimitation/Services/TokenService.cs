using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Market.Entity;
using Market.Implimitation.Interfaces;
using Market.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Market.Implimitation.Services;

public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<TokenService> _logger;

    public TokenService(IOptions<JwtOptions> jwtOptions, ILogger<TokenService> logger)
    {
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public string CreateToken(UserEntity user)
    {
        ArgumentNullException.ThrowIfNull(user);

        _logger.LogInformation("Creating JWT token for user. UserId: {UserId}, Email: {Email}", user.UserId, user.Email);

        if (string.IsNullOrWhiteSpace(_jwtOptions.Key))
            throw new InvalidOperationException("JWT Key is missing.");

        if (string.IsNullOrWhiteSpace(_jwtOptions.Issuer))
            throw new InvalidOperationException("JWT Issuer is missing.");

        if (string.IsNullOrWhiteSpace(_jwtOptions.Audience))
            throw new InvalidOperationException("JWT Audience is missing.");

        if (_jwtOptions.ExpireMinutes <= 0)
            throw new InvalidOperationException("JWT ExpireMinutes must be greater than zero.");
       
        var key = _jwtOptions.Key
                  ?? throw new InvalidOperationException("JWT Key is missing");

        var keyBytes = Encoding.UTF8.GetBytes(key);
        
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtOptions.ExpireMinutes),
            signingCredentials: credentials);

        _logger.LogInformation("JWT token created successfully for user. UserId: {UserId}", user.UserId);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}