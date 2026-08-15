using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.Users.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Users.Queries;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public GetUserByIdQueryHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<UserDto> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(query.UserId.ToString())
            ?? throw new NotFoundException(nameof(ApplicationUser), query.UserId);

        var roles = await _userManager.GetRolesAsync(user);
        return user.ToDto(roles);
    }
}

