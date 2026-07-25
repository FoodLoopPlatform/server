using FoodLoop.Application.Common.Models;
using MediatR;

namespace FoodLoop.Application.Features.Users.Commands;

public record DeleteUserCommand(Guid UserId) : IRequest<Result>;
