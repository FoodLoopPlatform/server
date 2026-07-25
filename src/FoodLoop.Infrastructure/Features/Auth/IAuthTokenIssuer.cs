using FoodLoop.Application.DTOs.Auth;
using FoodLoop.Infrastructure.Identity;

namespace FoodLoop.Infrastructure.Features.Auth;

/// <summary>
/// Issues a fresh access/refresh token pair for a user and persists the refresh token.
/// Shared by RegisterCommandHandler, LoginCommandHandler, and RefreshTokenCommandHandler
/// so the three don't each duplicate the same logic.
/// </summary>
public interface IAuthTokenIssuer
{
    Task<AuthResponse> IssueTokensAsync(
        ApplicationUser user, string? ipAddress, CancellationToken cancellationToken, string? refreshTokenValue = null);
}
