using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Auth;
using FoodLoop.Application.Features.Auth.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.API.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ILocalizationService _loc;

    public AuthController(ISender mediator, ILocalizationService loc)
    {
        _mediator = mediator;
        _loc = loc;
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>POST /auth/register — creates a new user account.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RegisterCommand(request, ClientIp), cancellationToken);
        if (!result.Success)
            return BadRequest(ApiResponse.Fail(result.Message ?? _loc["RegistrationFailed"], result.Errors));

        return CreatedAtAction(nameof(Register), ApiResponse<AuthResponse>.Ok(result.Data!));
    }

    /// <summary>POST /auth/login — returns access and refresh tokens.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new LoginCommand(request, ClientIp), cancellationToken);
        if (!result.Success)
            return Unauthorized(ApiResponse.Fail(result.Message ?? _loc["InvalidEmailOrPassword"]));

        return Ok(ApiResponse<AuthResponse>.Ok(result.Data!));
    }

    /// <summary>POST /auth/refresh — returns a new access token (rotates the refresh token).</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RefreshTokenCommand(request.RefreshToken, ClientIp), cancellationToken);
        if (!result.Success)
            return Unauthorized(ApiResponse.Fail(result.Message ?? _loc["Unauthorized"]));

        return Ok(ApiResponse<AuthResponse>.Ok(result.Data!));
    }

    /// <summary>POST /auth/logout — invalidates the current session's refresh token.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        await _mediator.Send(new LogoutCommand(request.RefreshToken), cancellationToken);
        return Ok(ApiResponse.Ok(_loc["LoggedOut"]));
    }

    /// <summary>POST /auth/resend-verification — re-sends the verification email
    /// for accounts still in PendingVerification status.</summary>
    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(
        [FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _mediator.Send(new ResendVerificationCommand(request.Email), cancellationToken);
        // Always 200 to avoid leaking account existence.
        return Ok(ApiResponse.Ok("If that account is pending verification, a new email has been sent."));
    }

    /// <summary>POST /auth/forgot-password — sends a password reset email.</summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ForgotPasswordCommand(request.Email), cancellationToken);
        // Always 200, regardless of whether the email exists, to avoid account enumeration.
        // In dev mode (no real email provider) the reset token is returned directly in the
        // response so it can be passed straight to POST /auth/reset-password.
        return Ok(ApiResponse<ForgotPasswordResult>.Ok(result.Data!));
    }

    /// <summary>POST /auth/reset-password — updates the user's password using a reset token.</summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ResetPasswordCommand(request), cancellationToken);
        if (!result.Success)
            return BadRequest(ApiResponse.Fail(result.Message ?? _loc["UnableToResetPassword"], result.Errors));

        return Ok(ApiResponse.Ok(_loc["PasswordReset"]));
    }
}
