namespace FoodLoop.Application.Common.Interfaces;

public record TokenPair(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt, DateTimeOffset RefreshTokenExpiresAt);

/// <summary>Generates and validates JWT access tokens and opaque refresh tokens.</summary>
public interface IJwtTokenService
{
    string GenerateAccessToken(Guid userId, string email, IEnumerable<string> roles);
    string GenerateRefreshToken();
    DateTimeOffset GetAccessTokenExpiry();
    DateTimeOffset GetRefreshTokenExpiry();
}
