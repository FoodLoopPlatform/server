using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Auth;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Services;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace FoodLoop.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IUnitOfWork unitOfWork,
        IJwtTokenService tokenService,
        IEmailService emailService,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        // "User" maps to the Consumer role; "Store Owner" and "Charity" both map to the
        // Merchant role, with the business/charity distinction captured on the Store itself
        // via StoreType — see create_account_account_type_selection / business_signup_step_1.
        var isBusinessAccount = request.AccountType is AccountType.StoreOwner or AccountType.Charity;

        if (isBusinessAccount && string.IsNullOrWhiteSpace(request.BusinessName))
        {
            return Result<AuthResponse>.Fail(
                "Business name is required for store owner and charity accounts.");
        }

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
        {
            return Result<AuthResponse>.Fail("Email is already registered.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.Name,
            PhoneNumber = request.PhoneNumber,
            // Business accounts start out under review; they can still log in to complete
            // the onboarding wizard (location + documents), matching verification_pending_step_3.
            Status = isBusinessAccount ? UserStatus.PendingVerification : UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        // User creation goes through UserManager (its own SaveChanges call against the
        // same DbContext), and the draft Store goes through the Unit of Work — wrapping
        // both in one transaction means a failure partway through leaves neither behind.
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result<AuthResponse>.Fail(
                    "Registration failed.",
                    createResult.Errors.Select(e => e.Description));
            }

            var assignedRole = isBusinessAccount ? AppRole.Merchant : AppRole.Consumer;
            await _userManager.AddToRoleAsync(user, assignedRole);

            if (isBusinessAccount)
            {
                _unitOfWork.Stores.Add(new Store
                {
                    OwnerId = user.Id,
                    Name = request.BusinessName!.Trim(),
                    StoreType = request.AccountType == AccountType.Charity ? StoreType.Charity : StoreType.Standard,
                    BusinessCategory = request.BusinessCategory,
                    VerificationStatus = VerificationStatus.Unverified,
                });
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        await _emailService.SendWelcomeEmailAsync(user.Email!, user.FullName, cancellationToken);

        var authResponse = await IssueTokensAsync(user, ipAddress, cancellationToken);
        return Result<AuthResponse>.Ok(authResponse);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
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

        var authResponse = await IssueTokensAsync(user, ipAddress, cancellationToken);
        return Result<AuthResponse>.Ok(authResponse);
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var existingToken = await _unitOfWork.RefreshTokens.GetByTokenAsync(refreshToken, cancellationToken);

        if (existingToken == null)
        {
            return Result<AuthResponse>.Fail("Invalid refresh token.");
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
                    sibling.RevokedByIp = ipAddress;
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogWarning("Refresh token reuse detected for user {UserId}. All sessions revoked.", existingToken.UserId);
            }

            return Result<AuthResponse>.Fail("Refresh token is no longer valid. Please log in again.");
        }

        var user = await _userManager.FindByIdAsync(existingToken.UserId.ToString());
        if (user == null || user.Status is UserStatus.Suspended or UserStatus.Banned)
        {
            return Result<AuthResponse>.Fail("Account is not available.");
        }

        // Rotate: revoke the used token and issue a brand new pair.
        var newToken = _tokenService.GenerateRefreshToken();
        existingToken.RevokedAt = DateTimeOffset.UtcNow;
        existingToken.RevokedByIp = ipAddress;
        existingToken.ReplacedByToken = newToken;

        var authResponse = await IssueTokensAsync(user, ipAddress, cancellationToken, newToken);
        return Result<AuthResponse>.Ok(authResponse);
    }

    public async Task<Result> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var existingToken = await _unitOfWork.RefreshTokens.GetByTokenAsync(refreshToken, cancellationToken);

        if (existingToken is { IsActive: true })
        {
            existingToken.RevokedAt = DateTimeOffset.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Ok();
    }

    public async Task<Result> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);

        // Always return success to avoid leaking whether an email is registered.
        if (user == null)
        {
            return Result.Ok();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        await _emailService.SendPasswordResetEmailAsync(email, token, cancellationToken);

        return Result.Ok();
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return Result.Fail("Invalid request.");
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            return Result.Fail("Unable to reset password.", result.Errors.Select(e => e.Description));
        }

        // Resetting the password invalidates all existing sessions.
        var tokens = await _unitOfWork.RefreshTokens.GetNonRevokedByUserIdAsync(user.Id, cancellationToken);

        foreach (var t in tokens)
        {
            t.RevokedAt = DateTimeOffset.UtcNow;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }

    private async Task<AuthResponse> IssueTokensAsync(
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
            User = new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                PhoneNumber = user.PhoneNumber,
                ProfileImage = user.ProfileImage,
                Language = user.Language,
                Status = user.Status.ToString(),
                OrderUpdatesEnabled = user.OrderUpdatesEnabled,
                MarketingNotificationsEnabled = user.MarketingNotificationsEnabled,
                Roles = roles.ToArray(),
                CreatedAt = user.CreatedAt,
            },
            AccessToken = accessToken,
            RefreshToken = refreshTokenString,
            AccessTokenExpiresAt = _tokenService.GetAccessTokenExpiry(),
        };
    }
}
