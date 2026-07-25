using FoodLoop.Application.Common.Models;
using MediatR;

namespace FoodLoop.Application.Features.Auth.Commands;

/// <summary>POST /auth/forgot-password — sends a password reset email if the address is registered.</summary>
public record ForgotPasswordCommand(string Email) : IRequest<Result>;
