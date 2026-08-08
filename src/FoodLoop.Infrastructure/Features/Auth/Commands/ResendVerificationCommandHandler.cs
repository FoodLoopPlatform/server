using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.Auth.Commands;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Auth.Commands;

public class ResendVerificationCommandHandler : IRequestHandler<ResendVerificationCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;

    public ResendVerificationCommandHandler(
        UserManager<ApplicationUser> userManager,
        IEmailService emailService)
    {
        _userManager = userManager;
        _emailService = emailService;
    }

    public async Task<Result> Handle(ResendVerificationCommand command, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(command.Email);

        // Always return success to avoid leaking whether an email is registered.
        // Send welcome email to any registered user regardless of status.
        if (user == null)
            return Result.Ok();

        await _emailService.SendWelcomeEmailAsync(user.Email!, user.FullName, cancellationToken);

        return Result.Ok();
    }
}

