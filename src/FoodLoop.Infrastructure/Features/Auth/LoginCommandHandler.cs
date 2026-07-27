using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Auth;
using FoodLoop.Application.Features.Auth.Commands;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Auth;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthTokenIssuer _tokenIssuer;

    public LoginCommandHandler(UserManager<ApplicationUser> userManager, IAuthTokenIssuer tokenIssuer)
    {
        _userManager = userManager;
        _tokenIssuer = tokenIssuer;
    }

    public async Task<Result<AuthResponse>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return Result<AuthResponse>.Fail("Invalid email or password.");
        }

        if (user.Status is UserStatus.Suspended or UserStatus.Banned)
        {
            return Result<AuthResponse>.Fail("This account is not active. Please contact support.");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            return Result<AuthResponse>.Fail("Invalid email or password.");
        }

        // Pending accounts have not yet been verified by an admin — return the user
        // record so the front-end can redirect to the verification-pending screen, but
        // do not issue tokens (they cannot access protected endpoints until approved).
        if (user.Status == UserStatus.PendingVerification)
        {
            var roles = await _userManager.GetRolesAsync(user);
            return Result<AuthResponse>.Ok(new AuthResponse
            {
                User = user.ToDto(roles),
                AccessToken = string.Empty,
                RefreshToken = string.Empty,
                AccessTokenExpiresAt = DateTimeOffset.MinValue,
            });
        }

        var authResponse = await _tokenIssuer.IssueTokensAsync(user, command.IpAddress, cancellationToken);
        return Result<AuthResponse>.Ok(authResponse);
    }
}
