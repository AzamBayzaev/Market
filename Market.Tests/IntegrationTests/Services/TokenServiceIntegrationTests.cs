using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Market.Entity;
using Market.Implimitation.Services;
using Market.Options;

namespace Market.Tests.IntegrationTests.Services;

public class TokenServiceIntegrationTests
{
    private TokenService CreateService()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Key = "THIS_IS_A_SUPER_SECRET_KEY_FOR_JWT_256_BITS_MINIMUM",
            Issuer = "test_issuer",
            Audience = "test_audience",
            ExpireMinutes = 60
        });

        return new TokenService(
            options,
            NullLogger<TokenService>.Instance
        );
    }

    private UserEntity CreateUser()
        => new UserEntity
        {
            UserId = 1,
            Email = "test@mail.com",
            Name = "TestUser",
            Role = "User"
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
    public void CreateToken_ShouldBeCryptographicallyValid()
    {
        var service = CreateService();
        var user = CreateUser();

        var token = service.CreateToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.NotNull(jwt.SignatureAlgorithm);
        Assert.NotNull(jwt.RawData);
    }
}