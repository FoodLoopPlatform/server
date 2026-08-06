using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.Users.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Users.Queries;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, UserDto>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public GetCurrentUserQueryHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<UserDto> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(query.UserId.ToString())
            ?? throw new NotFoundException(nameof(ApplicationUser), query.UserId);

        var roles = await _userManager.GetRolesAsync(user);
        return user.ToDto(roles);
    }
}

