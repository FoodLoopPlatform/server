using FluentAssertions;
using FoodLoop.Domain.Entities;
using Xunit;

namespace FoodLoop.Domain.Tests.Entities;

/// <summary>
/// RefreshToken has no dependencies, so these are pure, fast, no-mocking-required
/// tests - the cheapest and most valuable kind of unit test. Prefer this pattern
/// whenever you're testing domain logic that lives directly on an entity.
/// </summary>
public class RefreshTokenTests
{
    private static RefreshToken CreateToken(
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? revokedAt = null)
    {
        return new RefreshToken
        {
            UserId = Guid.NewGuid(),
            Token = "some-opaque-token",
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddDays(30),
            RevokedAt = revokedAt,
        };
    }

    [Fact]
    public void IsExpired_should_be_false_when_expiry_is_in_the_future()
    {
        var token = CreateToken(expiresAt: DateTimeOffset.UtcNow.AddMinutes(5));

        token.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_should_be_true_when_expiry_is_in_the_past()
    {
        var token = CreateToken(expiresAt: DateTimeOffset.UtcNow.AddMinutes(-5));

        token.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsRevoked_should_be_false_when_RevokedAt_is_null()
    {
        var token = CreateToken(revokedAt: null);

        token.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public void IsRevoked_should_be_true_when_RevokedAt_is_set()
    {
        var token = CreateToken(revokedAt: DateTimeOffset.UtcNow);

        token.IsRevoked.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, false, true)]  // not expired, not revoked -> active
    [InlineData(true, false, false)]  // expired -> not active
    [InlineData(false, true, false)]  // revoked -> not active
    [InlineData(true, true, false)]   // expired and revoked -> not active
    public void IsActive_should_reflect_expiry_and_revocation_state(
        bool expired, bool revoked, bool expectedActive)
    {
        var token = CreateToken(
            expiresAt: expired ? DateTimeOffset.UtcNow.AddMinutes(-1) : DateTimeOffset.UtcNow.AddMinutes(1),
            revokedAt: revoked ? DateTimeOffset.UtcNow : null);

        token.IsActive.Should().Be(expectedActive);
    }
}
