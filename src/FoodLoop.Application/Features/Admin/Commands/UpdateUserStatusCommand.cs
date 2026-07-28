using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.DTOs.Users;
using MediatR;

namespace FoodLoop.Application.Features.Admin.Commands;

/// <summary>PATCH /admin/users/{id}/status — suspend, ban, or reactivate a user account.</summary>
public record UpdateUserStatusCommand(Guid UserId, UpdateUserStatusRequest Request) : IRequest<Result<UserDto>>;
