using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Market.Entity;
using Market.Implimitation.Services;
using Market.Options;

public class TokenServiceTests
{
    private TokenService CreateService()
    {
        var options = Options.Create(new JwtOptions
        {
            Key = new string('a', 64),
            Issuer = "test_issuer",
            Audience = "test_audience",
            ExpireMinutes = 60
        });

        return new TokenService(options, NullLogger<TokenService>.Instance);
    }

    private UserEntity CreateUser()
        => new UserEntity
        {
            UserId = 1,
            Email = "test@mail.com",
            Name = "TestUser",
            Role = "User",
            PasswordHash = "hash"
        };

    [Fact]
    public void CreateToken_ShouldReturnValidJwt()
    {
        var service = CreateService();
        var user = CreateUser();

        var token = service.CreateToken(user);

        Assert.False(string.IsNullOrWhiteSpace(token));

        var handler = new JwtSecurityTokenHandler();
        Assert.True(handler.CanReadToken(token));
    }

    [Fact]
    public void CreateToken_ShouldContainCorrectClaims()
    {
        var service = CreateService();
        var user = CreateUser();

        var token = service.CreateToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Contains(jwt.Claims, c =>
            c.Type == ClaimTypes.NameIdentifier && c.Value == "1");

        Assert.Contains(jwt.Claims, c =>
            c.Type == ClaimTypes.Email && c.Value == "test@mail.com");

        Assert.Contains(jwt.Claims, c =>
            c.Type == ClaimTypes.Name && c.Value == "TestUser");

        Assert.Contains(jwt.Claims, c =>
            c.Type == ClaimTypes.Role && c.Value == "User");
    }

    [Fact]
    public void CreateToken_ShouldHaveCorrectIssuerAndAudience()
    {
        var service = CreateService();
        var user = CreateUser();

        var token = service.CreateToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("test_issuer", jwt.Issuer);
        Assert.Contains("test_audience", jwt.Audiences);
    }

    [Fact]
    public void CreateToken_ShouldFail_WhenUserIsNull()
    {
        var service = CreateService();

        Assert.Throws<ArgumentNullException>(() =>
            service.CreateToken(null!));
    }
}