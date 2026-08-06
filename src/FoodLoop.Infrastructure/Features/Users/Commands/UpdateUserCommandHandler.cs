using FoodLoop.Application.Common.Exceptions;
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

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Result<UserDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILocalizationService _loc;

    public UpdateUserCommandHandler(UserManager<ApplicationUser> userManager, ILocalizationService loc)
    {
        _userManager = userManager;
        _loc = loc;
    }

    public async Task<Result<UserDto>> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(command.UserId.ToString())
            ?? throw new NotFoundException(nameof(ApplicationUser), command.UserId);

        var request = command.Request;

        if (request.Role != null && !AppRole.All.Contains(request.Role))
        {
            return Result<UserDto>.Fail(
                _loc["InvalidRole", request.Role, string.Join(", ", AppRole.All)]);
        }

        if (request.Status != null && !Enum.TryParse<UserStatus>(request.Status, true, out _))
        {
            return Result<UserDto>.Fail(
                _loc["InvalidUserStatus", request.Status, string.Join(", ", Enum.GetNames<UserStatus>())]);
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && !request.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _userManager.FindByEmailAsync(request.Email);
            if (existing != null)
            {
                return Result<UserDto>.Fail(_loc["EmailAlreadyRegistered"]);
            }
            user.Email = request.Email;
            user.UserName = request.Email;
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
            user.FullName = request.FullName.Trim();

        if (request.PhoneNumber != null)
            user.PhoneNumber = request.PhoneNumber;

        if (!string.IsNullOrWhiteSpace(request.Language))
            user.Language = request.Language == "ar" ? "ar" : "en";

        if (request.Status != null)
        {
            Enum.TryParse<UserStatus>(request.Status, true, out var status);
            user.Status = status;
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Result<UserDto>.Fail(
                _loc["FailedToUpdateUser"],
                updateResult.Errors.Select(e => e.Description));
        }

        if (request.Role != null)
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Count > 0)
                await _userManager.RemoveFromRolesAsync(user, currentRoles);

            await _userManager.AddToRoleAsync(user, request.Role);
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Result<UserDto>.Ok(user.ToDto(roles));
    }
}

