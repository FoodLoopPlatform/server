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

namespace FoodLoop.Infrastructure.Features.Auth.Commands;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ILocalizationService _loc;
    private readonly IAuditLogService _auditLogService;
    private readonly IRealTimeNotificationService _notificationService;

    public RegisterCommandHandler(
        UserManager<ApplicationUser> userManager,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ILocalizationService loc,
        IAuditLogService auditLogService,
        IRealTimeNotificationService notificationService)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _loc = loc;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
    }

    public async Task<Result<AuthResponse>> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        // Admin is never self-registerable â€” it can only be granted by an existing admin
        // via UsersController.
        if (!AppRole.SelfRegisterable.Contains(request.Role))
        {
            return Result<AuthResponse>.Fail(
                _loc["InvalidRole", request.Role, string.Join(", ", AppRole.SelfRegisterable)]);
        }

        // Only Merchant accounts hold a physical Organization and go through Organization Onboarding.
        // Charity role is treated as a premium customer account without a Organization.
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
        // same DbContext), and the draft Organization goes through the Unit of Work â€” wrapping
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

            Organization? org = null;
            if (isBusinessAccount || isCharityAccount)
            {
                org = new Organization
                {
                    OwnerId = user.Id,
                    Name = request.BusinessName!.Trim(),
                    BusinessCategory = request.BusinessCategory,
                    VerificationStatus = VerificationStatus.Unverified,
                };
                _unitOfWork.Organizations.Add(org);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            if (isBusinessAccount || isCharityAccount)
            {
                await _auditLogService.LogAsync(
                    user.Id,
                    org?.Id,
                    "AccountCreated",
                    request.Role == AppRole.Merchant ? "Merchant Account Created" : "Charity Account Created",
                    $"New {(request.Role == AppRole.Merchant ? "merchant" : "charity")} account registered with email {user.Email} for organization '{request.BusinessName!.Trim()}'.",
                    command.IpAddress,
                    cancellationToken);
            }
            else
            {
                await _auditLogService.LogAsync(
                    user.Id,
                    null,
                    "AccountCreated",
                    "Account Created",
                    $"New account registered with email {user.Email}.",
                    command.IpAddress,
                    cancellationToken);
            }

            if (_notificationService != null)
            {
                await _notificationService.SendNotificationToRoleAsync(
                    "Admin",
                    "NotifNewUserRegisteredTitle",
                    isBusinessOrCharityRole ? "NotifNewBusinessRegisteredBody" : "NotifNewUserRegisteredBody",
                    "AccountCreated",
                    isBusinessOrCharityRole
                        ? new object[] { request.Role.ToLower(), user.Email!, request.BusinessName!.Trim() }
                        : new object[] { user.Email!, user.FullName },
                    "User",
                    user.Id,
                    cancellationToken);
            }
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        // Send role-appropriate email:
        // - Customers are active immediately -> welcome email
        // - Merchants/Charities are pending admin review -> pending review notification
        if (isBusinessOrCharityRole)
        {
            var orgName = request.BusinessName!.Trim();
            await _emailService.SendPendingReviewEmailAsync(user.Email!, user.FullName, orgName, cancellationToken);
        }
        else
        {
            await _emailService.SendWelcomeEmailAsync(user.Email!, user.FullName, cancellationToken);
        }

        return Result<AuthResponse>.Ok(new AuthResponse
        {
            User = user.ToDto(new[] { request.Role }),
            AccessToken = string.Empty,
            RefreshToken = string.Empty,
            AccessTokenExpiresAt = DateTimeOffset.MinValue
        });
    }
}


