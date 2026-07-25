using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Auth;
using MediatR;

namespace FoodLoop.Application.Features.Auth.Commands;

/// <summary>POST /auth/reset-password — sets a new password from a valid reset token
/// and revokes all of the user's existing sessions.</summary>
public record ResetPasswordCommand(ResetPasswordRequest Request) : IRequest<Result>;
