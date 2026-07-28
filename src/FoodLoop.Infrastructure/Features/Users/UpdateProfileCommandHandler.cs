using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.Users.Commands;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Users;

public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, UserDto>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UpdateProfileCommandHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<UserDto> Handle(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(command.UserId.ToString())
            ?? throw new NotFoundException(nameof(ApplicationUser), command.UserId);

        var request = command.Request;

        if (!string.IsNullOrWhiteSpace(request.Name))
            user.FullName = request.Name.Trim();

        if (request.ProfileImage != null)
            user.ProfileImage = request.ProfileImage;

        if (!string.IsNullOrWhiteSpace(request.PreferredLanguage))
            user.Language = request.PreferredLanguage == "ar" ? "ar" : "en";

        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        return user.ToDto(roles);
    }
}
