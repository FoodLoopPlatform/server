using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Admin.Commands;

public class UpdateUserStatusCommandHandler : IRequestHandler<UpdateUserStatusCommand, Result<UserDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILocalizationService _loc;
    private readonly IAuditLogService _auditLogService;

    public UpdateUserStatusCommandHandler(UserManager<ApplicationUser> userManager, ILocalizationService loc, IAuditLogService auditLogService)
    {
        _userManager = userManager;
        _loc = loc;
        _auditLogService = auditLogService;
    }

    public async Task<Result<UserDto>> Handle(UpdateUserStatusCommand command, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(command.UserId.ToString())
            ?? throw new NotFoundException(nameof(ApplicationUser), command.UserId);

        if (!Enum.TryParse<UserStatus>(command.Request.Status, true, out var newStatus))
            return Result<UserDto>.Fail(_loc["InvalidUserStatus", command.Request.Status,
                string.Join(", ", Enum.GetNames<UserStatus>())]);

        user.Status = newStatus;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return Result<UserDto>.Fail(_loc["FailedToUpdateUser"],
                result.Errors.Select(e => e.Description));

        await _auditLogService.LogAsync(
            user.Id,
            null,
            "UserStatusUpdated",
            "User Account Status Changed",
            $"Administrator updated status of user '{user.Email}' to {newStatus}.",
            null,
            cancellationToken);

        var roles = await _userManager.GetRolesAsync(user);
        return Result<UserDto>.Ok(user.ToDto(roles));
    }
}

