using FoodLoop.Application.Common.Models;
using MediatR;

namespace FoodLoop.Application.Features.Auth.Commands;

/// <summary>POST /auth/logout — revokes the given refresh token.</summary>
public record LogoutCommand(string RefreshToken) : IRequest<Result>;
