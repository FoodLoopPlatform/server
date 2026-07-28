using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.Users.Commands;
using FoodLoop.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Users;

public class UpdatePreferencesCommandHandler : IRequestHandler<UpdatePreferencesCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UpdatePreferencesCommandHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Result> Handle(UpdatePreferencesCommand command, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(command.UserId.ToString())
            ?? throw new NotFoundException(nameof(ApplicationUser), command.UserId);

        var request = command.Request;

        if (request.OrderUpdatesEnabled.HasValue)
            user.OrderUpdatesEnabled = request.OrderUpdatesEnabled.Value;

        if (request.MarketingNotificationsEnabled.HasValue)
            user.MarketingNotificationsEnabled = request.MarketingNotificationsEnabled.Value;

        if (!string.IsNullOrWhiteSpace(request.PreferredLanguage))
            user.Language = request.PreferredLanguage == "ar" ? "ar" : "en";

        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userManager.UpdateAsync(user);

        return Result.Ok();
    }
}
