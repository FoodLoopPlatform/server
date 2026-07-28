using FoodLoop.Application.Common.Models;
using MediatR;

namespace FoodLoop.Application.Features.Auth.Commands;

/// <summary>POST /auth/resend-verification — re-sends the welcome / verification email
/// for accounts that are still PendingVerification.</summary>
public record ResendVerificationCommand(string Email) : IRequest<Result>;
