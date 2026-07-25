using FoodLoop.Application.DTOs.Users;
using MediatR;

namespace FoodLoop.Application.Features.Users.Commands;

/// <summary>PATCH /users/me — updates the authenticated user's profile fields.</summary>
public record UpdateProfileCommand(Guid UserId, UpdateProfileRequest Request) : IRequest<UserDto>;
