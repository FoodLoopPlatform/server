using FoodLoop.Domain.Common;

namespace FoodLoop.Domain.Entities;

/// <summary>
/// Persisted refresh token used to rotate JWT access tokens without requiring
/// the user to re-authenticate. Tokens are single-use: on refresh, the old
/// token is revoked and replaced by a new one (rotation + reuse detection).
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }

    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }

    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    public bool IsActive => !IsRevoked && !IsExpired;
}
