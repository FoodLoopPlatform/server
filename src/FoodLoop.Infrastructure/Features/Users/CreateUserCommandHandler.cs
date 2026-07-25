using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.Users.Commands;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Users;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<UserDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public CreateUserCommandHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Result<UserDto>> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        if (!AppRole.All.Contains(request.Role))
        {
            return Result<UserDto>.Fail(
                $"Invalid role '{request.Role}'. Expected one of: {string.Join(", ", AppRole.All)}.");
        }

        if (!Enum.TryParse<UserStatus>(request.Status, true, out var userStatus))
        {
            return Result<UserDto>.Fail(
                $"Invalid user status '{request.Status}'. Expected values: {string.Join(", ", Enum.GetNames<UserStatus>())}.");
        }

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
        {
            return Result<UserDto>.Fail("Email is already registered.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Status = userStatus,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return Result<UserDto>.Fail(
                "Failed to create user.",
                createResult.Errors.Select(e => e.Description));
        }

        await _userManager.AddToRoleAsync(user, request.Role);

        var roles = new List<string> { request.Role };
        return Result<UserDto>.Ok(user.ToDto(roles));
    }
}
