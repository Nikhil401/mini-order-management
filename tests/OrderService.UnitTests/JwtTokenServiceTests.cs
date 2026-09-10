using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OrderService.Services;
using Xunit;

namespace OrderService.UnitTests;

public class JwtTokenServiceTests
{
    [Fact]
    public void CreateToken_ContainsExpectedIdentityClaims()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:Key"] = "test-signing-key-that-is-long-enough"
            })
            .Build();

        var token = new JwtTokenService(configuration).CreateToken("nikhil", "Admin");
        var parsedToken = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("test-issuer", parsedToken.Issuer);
        Assert.Contains("test-audience", parsedToken.Audiences);
        Assert.Equal("nikhil", parsedToken.Claims.Single(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal("Admin", parsedToken.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void CreateToken_IsSignedWithConfiguredKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:Key"] = "test-signing-key-that-is-long-enough"
            })
            .Build();

        var token = new JwtTokenService(configuration).CreateToken("nikhil", "Admin");
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "test-issuer",
            ValidateAudience = true,
            ValidAudience = "test-audience",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes("test-signing-key-that-is-long-enough"))
        };

        var principal = new JwtSecurityTokenHandler().ValidateToken(
            token,
            validationParameters,
            out _);

        Assert.Equal("nikhil", principal.Identity?.Name);
    }
}
