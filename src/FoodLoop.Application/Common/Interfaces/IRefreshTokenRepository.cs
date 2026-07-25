using FoodLoop.Domain.Entities;

namespace FoodLoop.Application.Common.Interfaces;

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Every token for a user that hasn't been revoked yet (may still be expired —
    /// callers that care about IsActive filter that themselves). Used for reuse-detection
    /// revocation and for invalidating sessions on password reset.</summary>
    Task<IReadOnlyList<RefreshToken>> GetNonRevokedByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
