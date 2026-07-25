using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Users;
using MediatR;

namespace FoodLoop.Application.Features.Users.Commands;

/// <summary>PATCH /users/me/preferences — updates notification and language settings.</summary>
public record UpdatePreferencesCommand(Guid UserId, UpdatePreferencesRequest Request) : IRequest<Result>;
