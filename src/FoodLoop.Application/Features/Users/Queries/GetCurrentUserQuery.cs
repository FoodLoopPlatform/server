using FoodLoop.Application.DTOs.Users;
using MediatR;

namespace FoodLoop.Application.Features.Users.Queries;

/// <summary>GET /users/me — the authenticated user's own profile.</summary>
public record GetCurrentUserQuery(Guid UserId) : IRequest<UserDto>;
