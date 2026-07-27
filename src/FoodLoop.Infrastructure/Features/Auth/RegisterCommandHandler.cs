using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Auth;
using FoodLoop.Application.Features.Auth.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Auth;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IAuthTokenIssuer _tokenIssuer;
    private readonly ILocalizationService _loc;

    public RegisterCommandHandler(
        UserManager<ApplicationUser> userManager,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        IAuthTokenIssuer tokenIssuer,
        ILocalizationService loc)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _tokenIssuer = tokenIssuer;
        _loc = loc;
    }

    public async Task<Result<AuthResponse>> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        // Admin is never self-registerable — it can only be granted by an existing admin
        // via UsersController.
        if (!AppRole.SelfRegisterable.Contains(request.Role))
        {
            return Result<AuthResponse>.Fail(
                _loc["InvalidRole", request.Role, string.Join(", ", AppRole.SelfRegisterable)]);
        }

        // Only Merchant accounts hold a physical Store and go through Store Onboarding.
        // Charity role is treated as a premium customer account without a Store.
        var isBusinessAccount = request.Role == AppRole.Merchant;
        var isCharityAccount = request.Role == AppRole.Charity;
        var isBusinessOrCharityRole = isBusinessAccount || isCharityAccount;

        if (isBusinessOrCharityRole && string.IsNullOrWhiteSpace(request.BusinessName))
        {
            return Result<AuthResponse>.Fail(_loc["BusinessNameRequired"]);
        }

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
        {
            return Result<AuthResponse>.Fail(_loc["EmailAlreadyRegistered"]);
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var phoneExists = _userManager.Users
                .Any(u => u.PhoneNumber == request.PhoneNumber);
            if (phoneExists)
            {
                return Result<AuthResponse>.Fail(_loc["PhoneAlreadyRegistered"]);
            }
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = isCharityAccount && !string.IsNullOrWhiteSpace(request.BusinessName)
                ? request.BusinessName.Trim()
                : request.Name,
            PhoneNumber = request.PhoneNumber,
            Language = request.Language == "ar" ? "ar" : "en",
            // Only customer accounts are verified immediately; merchants and charities are pending.
            Status = request.Role == AppRole.Customer ? UserStatus.Active : UserStatus.PendingVerification,
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
                    _loc["RegistrationFailed"],
                    createResult.Errors.Select(e => e.Description));
            }

            await _userManager.AddToRoleAsync(user, request.Role);

            if (isBusinessAccount)
            {
                _unitOfWork.Stores.Add(new Store
                {
                    OwnerId = user.Id,
                    Name = request.BusinessName!.Trim(),
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

        // If the account is unverified, do not return access and refresh tokens
        if (user.Status == UserStatus.PendingVerification)
        {
            return Result<AuthResponse>.Ok(new AuthResponse
            {
                User = user.ToDto(new[] { request.Role }),
                AccessToken = string.Empty,
                RefreshToken = string.Empty,
                AccessTokenExpiresAt = DateTimeOffset.MinValue
            });
        }

        var authResponse = await _tokenIssuer.IssueTokensAsync(user, command.IpAddress, cancellationToken);
        return Result<AuthResponse>.Ok(authResponse);
    }
}
