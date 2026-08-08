using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.Auth.Commands;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Features.Auth.Commands;

public class ResendVerificationCommandHandler : IRequestHandler<ResendVerificationCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly ApplicationDbContext _db;

    public ResendVerificationCommandHandler(
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _emailService = emailService;
        _db = db;
    }

    public async Task<Result> Handle(ResendVerificationCommand command, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(command.Email);

        // Always return success to avoid leaking whether an email is registered.
        if (user == null)
            return Result.Ok();

        var roles = await _userManager.GetRolesAsync(user);
        bool isPendingBusiness = roles.Contains(AppRole.Merchant) || roles.Contains(AppRole.Charity);

        if (isPendingBusiness)
        {
            // Look up organization name for context in the email
            var org = await _db.Organizations
                .FirstOrDefaultAsync(o => o.OwnerId == user.Id && !o.IsDeleted, cancellationToken);
            var orgName = org?.Name ?? user.FullName;
            await _emailService.SendPendingReviewEmailAsync(user.Email!, user.FullName, orgName, cancellationToken);
        }
        else
        {
            await _emailService.SendWelcomeEmailAsync(user.Email!, user.FullName, cancellationToken);
        }

        return Result.Ok();
    }
}
