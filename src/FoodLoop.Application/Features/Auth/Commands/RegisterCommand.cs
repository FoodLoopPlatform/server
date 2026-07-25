using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Auth;
using MediatR;

namespace FoodLoop.Application.Features.Auth.Commands;

/// <summary>POST /auth/register — creates a new user account (and, for business
/// account types, a draft Store) and returns an issued token pair.</summary>
public record RegisterCommand(RegisterRequest Request, string? IpAddress) : IRequest<Result<AuthResponse>>;
