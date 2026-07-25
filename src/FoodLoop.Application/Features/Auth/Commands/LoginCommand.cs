using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Auth;
using MediatR;

namespace FoodLoop.Application.Features.Auth.Commands;

/// <summary>POST /auth/login — validates credentials and returns an issued token pair.</summary>
public record LoginCommand(LoginRequest Request, string? IpAddress) : IRequest<Result<AuthResponse>>;
