using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Auth;
using MediatR;

namespace FoodLoop.Application.Features.Auth.Commands;

/// <summary>POST /auth/refresh — rotates a refresh token and returns a new token pair.</summary>
public record RefreshTokenCommand(string RefreshToken, string? IpAddress) : IRequest<Result<AuthResponse>>;
