using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.Users.Commands;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Users.Commands;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<UserDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILocalizationService _loc;
    private readonly IRealTimeNotificationService _notificationService;

    public CreateUserCommandHandler(
        UserManager<ApplicationUser> userManager,
        ILocalizationService loc,
        IRealTimeNotificationService notificationService)
    {
        _userManager = userManager;
        _loc = loc;
        _notificationService = notificationService;
    }

    public async Task<Result<UserDto>> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        if (!AppRole.All.Contains(request.Role))
        {
            return Result<UserDto>.Fail(
                _loc["InvalidRole", request.Role, string.Join(", ", AppRole.All)]);
        }

        if (!Enum.TryParse<UserStatus>(request.Status, true, out var userStatus))
        {
            return Result<UserDto>.Fail(
                _loc["InvalidUserStatus", request.Status, string.Join(", ", Enum.GetNames<UserStatus>())]);
        }

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
        {
            return Result<UserDto>.Fail(_loc["EmailAlreadyRegistered"]);
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
                _loc["FailedToCreateUser"],
                createResult.Errors.Select(e => e.Description));
        }

        await _userManager.AddToRoleAsync(user, request.Role);

        if (_notificationService != null)
        {
            await _notificationService.SendNotificationToRoleAsync(
                "Admin",
                "New User Registered",
                $"New account registered: {user.Email} ({user.FullName}).",
                "AccountCreated",
                "User",
                user.Id,
                cancellationToken);
        }

        var roles = new List<string> { request.Role };
        return Result<UserDto>.Ok(user.ToDto(roles));
    }
}

