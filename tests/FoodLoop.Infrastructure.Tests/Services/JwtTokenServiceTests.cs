using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using FoodLoop.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Services;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService(JwtSettings? settings = null)
    {
        settings ??= new JwtSettings
        {
            Issuer = "FoodLoop.Tests",
            Audience = "FoodLoop.Tests.Client",
            // HMAC-SHA256 needs a key of at least 256 bits (32 bytes).
            Secret = "this-is-a-test-only-signing-secret-not-for-prod!",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 30,
        };

        return new JwtTokenService(Options.Create(settings));
    }

    [Fact]
    public void GenerateAccessToken_should_embed_the_user_id_email_and_roles_as_claims()
    {
        var service = CreateService();
        var userId = Guid.NewGuid();

        var token = service.GenerateAccessToken(userId, "person@example.com", new[] { "Consumer", "Merchant" });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Subject.Should().Be(userId.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "person@example.com");
        jwt.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .Should().BeEquivalentTo("Consumer", "Merchant");
    }

    [Fact]
    public void GenerateAccessToken_should_set_issuer_and_audience_from_settings()
    {
        var settings = new JwtSettings
        {
            Issuer = "custom-issuer",
            Audience = "custom-audience",
            Secret = "this-is-a-test-only-signing-secret-not-for-prod!",
        };
        var service = CreateService(settings);

        var token = service.GenerateAccessToken(Guid.NewGuid(), "person@example.com", Array.Empty<string>());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Issuer.Should().Be("custom-issuer");
        jwt.Audiences.Should().Contain("custom-audience");
    }

    [Fact]
    public void GenerateRefreshToken_should_return_unique_values_on_each_call()
    {
        var service = CreateService();

        var first = service.GenerateRefreshToken();
        var second = service.GenerateRefreshToken();

        first.Should().NotBe(second);
    }

    [Fact]
    public void GetAccessTokenExpiry_should_be_settings_driven_minutes_from_now()
    {
        var service = CreateService(new JwtSettings
        {
            Issuer = "i",
            Audience = "a",
            Secret = "this-is-a-test-only-signing-secret-not-for-prod!",
            AccessTokenExpirationMinutes = 42,
        });

        var expiry = service.GetAccessTokenExpiry();

        expiry.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(42), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GetRefreshTokenExpiry_should_be_settings_driven_days_from_now()
    {
        var service = CreateService(new JwtSettings
        {
            Issuer = "i",
            Audience = "a",
            Secret = "this-is-a-test-only-signing-secret-not-for-prod!",
            RefreshTokenExpirationDays = 7,
        });

        var expiry = service.GetRefreshTokenExpiry();

        expiry.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
    }
}
