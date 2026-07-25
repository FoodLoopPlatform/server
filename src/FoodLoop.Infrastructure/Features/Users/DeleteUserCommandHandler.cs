using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.Users.Commands;
using FoodLoop.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Users;

public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public DeleteUserCommandHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Result> Handle(DeleteUserCommand command, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(command.UserId.ToString())
            ?? throw new NotFoundException(nameof(ApplicationUser), command.UserId);

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return Result.Fail(
                "Failed to delete user. The user might have active relationships (e.g. store, orders) preventing deletion.",
                result.Errors.Select(e => e.Description));
        }

        return Result.Ok();
    }
}
