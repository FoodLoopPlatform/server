using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Auth;
using FoodLoop.Application.Features.Auth.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Auth;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IAuthTokenIssuer _tokenIssuer;

    public RegisterCommandHandler(
        UserManager<ApplicationUser> userManager,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        IAuthTokenIssuer tokenIssuer)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _tokenIssuer = tokenIssuer;
    }

    public async Task<Result<AuthResponse>> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        // Admin is never self-registerable — it can only be granted by an existing admin
        // via UsersController.
        if (!AppRole.SelfRegisterable.Contains(request.Role))
        {
            return Result<AuthResponse>.Fail(
                $"Invalid role '{request.Role}'. Expected one of: {string.Join(", ", AppRole.SelfRegisterable)}.");
        }

        // Merchant and Charity are both business accounts, distinguished on the Store itself
        // via StoreType — see create_account_account_type_selection / business_signup_step_1.
        var isBusinessAccount = request.Role is AppRole.Merchant or AppRole.Charity;

        if (isBusinessAccount && string.IsNullOrWhiteSpace(request.BusinessName))
        {
            return Result<AuthResponse>.Fail(
                "Business name is required for merchant and charity accounts.");
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

            await _userManager.AddToRoleAsync(user, request.Role);

            if (isBusinessAccount)
            {
                _unitOfWork.Stores.Add(new Store
                {
                    OwnerId = user.Id,
                    Name = request.BusinessName!.Trim(),
                    StoreType = request.Role == AppRole.Charity ? StoreType.Charity : StoreType.Standard,
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

        var authResponse = await _tokenIssuer.IssueTokensAsync(user, command.IpAddress, cancellationToken);
        return Result<AuthResponse>.Ok(authResponse);
    }
}
