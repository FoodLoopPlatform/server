using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Users;
using MediatR;

namespace FoodLoop.Application.Features.Users.Commands;

public record CreateUserCommand(CreateUserRequest Request) : IRequest<Result<UserDto>>;
