using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Auth;
using FoodLoop.Application.Features.Auth.Commands;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace FoodLoop.Infrastructure.Features.Auth;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _tokenService;
    private readonly IAuthTokenIssuer _tokenIssuer;
    private readonly ILocalizationService _loc;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        UserManager<ApplicationUser> userManager,
        IUnitOfWork unitOfWork,
        IJwtTokenService tokenService,
        IAuthTokenIssuer tokenIssuer,
        ILocalizationService loc,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _tokenIssuer = tokenIssuer;
        _loc = loc;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var existingToken = await _unitOfWork.RefreshTokens.GetByTokenAsync(command.RefreshToken, cancellationToken);

        if (existingToken == null)
        {
            return Result<AuthResponse>.Fail(_loc["InvalidRefreshToken"]);
        }

        if (!existingToken.IsActive)
        {
            // Reuse of a revoked/expired token is treated as a potential compromise:
            // revoke every other active token for this user as a precaution.
            if (existingToken.IsRevoked)
            {
                var siblingTokens = await _unitOfWork.RefreshTokens.GetNonRevokedByUserIdAsync(existingToken.UserId, cancellationToken);

                foreach (var sibling in siblingTokens)
                {
                    sibling.RevokedAt = DateTimeOffset.UtcNow;
                    sibling.RevokedByIp = command.IpAddress;
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogWarning("Refresh token reuse detected for user {UserId}. All sessions revoked.", existingToken.UserId);
            }

            return Result<AuthResponse>.Fail(_loc["RefreshTokenExpired"]);
        }

        var user = await _userManager.FindByIdAsync(existingToken.UserId.ToString());
        if (user == null || user.Status is UserStatus.Suspended or UserStatus.Banned)
        {
            return Result<AuthResponse>.Fail(_loc["AccountNotAvailable"]);
        }

        // Rotate: revoke the used token and issue a brand new pair.
        var newToken = _tokenService.GenerateRefreshToken();
        existingToken.RevokedAt = DateTimeOffset.UtcNow;
        existingToken.RevokedByIp = command.IpAddress;
        existingToken.ReplacedByToken = newToken;

        var authResponse = await _tokenIssuer.IssueTokensAsync(user, command.IpAddress, cancellationToken, newToken);
        return Result<AuthResponse>.Ok(authResponse);
    }
}
