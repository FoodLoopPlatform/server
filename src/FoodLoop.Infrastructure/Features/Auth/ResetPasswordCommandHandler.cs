using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.Auth.Commands;
using FoodLoop.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Auth;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;

    public ResetPasswordCommandHandler(UserManager<ApplicationUser> userManager, IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

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
}
