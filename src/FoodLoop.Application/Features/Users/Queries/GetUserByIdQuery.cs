using FoodLoop.Application.DTOs.Users;
using MediatR;

namespace FoodLoop.Application.Features.Users.Queries;

public record GetUserByIdQuery(Guid UserId) : IRequest<UserDto>;
