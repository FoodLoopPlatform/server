using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.Auth.Commands;
using FoodLoop.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Auth;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result<ForgotPasswordResult>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;

    public ForgotPasswordCommandHandler(UserManager<ApplicationUser> userManager, IEmailService emailService)
    {
        _userManager = userManager;
        _emailService = emailService;
    }

    public async Task<Result<ForgotPasswordResult>> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(command.Email);

        // Always return success to avoid leaking whether an email is registered.
        if (user == null)
        {
            return Result<ForgotPasswordResult>.Ok(new ForgotPasswordResult());
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        await _emailService.SendPasswordResetEmailAsync(command.Email, token, cancellationToken);

        // When no real email provider is wired in (dev/staging) surface the token in the
        // response body so the frontend can call /auth/reset-password without server-log access.
        var debugToken = _emailService.IsDevStub ? token : null;

        return Result<ForgotPasswordResult>.Ok(new ForgotPasswordResult { DebugToken = debugToken });
    }
}
