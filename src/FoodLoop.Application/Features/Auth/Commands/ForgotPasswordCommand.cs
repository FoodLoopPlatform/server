using FoodLoop.Application.Common.Models;
using MediatR;

namespace FoodLoop.Application.Features.Auth.Commands;

/// <summary>POST /auth/forgot-password — sends a password reset email if the address is registered.</summary>
public record ForgotPasswordCommand(string Email) : IRequest<Result<ForgotPasswordResult>>;

/// <summary>Carries the reset token back to the caller in development/no-email-provider mode.
/// In production (when a real email provider is wired in) <see cref="DebugToken"/> is null
/// and the token is delivered only via email.</summary>
public class ForgotPasswordResult
{
    /// <summary>Populated only when no real email provider is configured (dev/staging).
    /// Pass this directly to POST /auth/reset-password as the <c>token</c> field.</summary>
    public string? DebugToken { get; init; }
}
