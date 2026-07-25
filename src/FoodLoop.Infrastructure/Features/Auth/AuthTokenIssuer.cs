using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Auth;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Mappings;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Auth;

internal class AuthTokenIssuer : IAuthTokenIssuer
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _tokenService;

    public AuthTokenIssuer(UserManager<ApplicationUser> userManager, IUnitOfWork unitOfWork, IJwtTokenService tokenService)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> IssueTokensAsync(
        ApplicationUser user, string? ipAddress, CancellationToken cancellationToken, string? refreshTokenValue = null)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.Email!, roles);

        var refreshTokenString = refreshTokenValue ?? _tokenService.GenerateRefreshToken();
        var refreshTokenExpiry = _tokenService.GetRefreshTokenExpiry();

        _unitOfWork.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenString,
            ExpiresAt = refreshTokenExpiry,
            CreatedByIp = ipAddress,
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResponse
        {
            User = user.ToDto(roles),
            AccessToken = accessToken,
            RefreshToken = refreshTokenString,
            AccessTokenExpiresAt = _tokenService.GetAccessTokenExpiry(),
        };
    }
}
