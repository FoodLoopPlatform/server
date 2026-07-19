using FoodLoop.API.Common;
using FoodLoop.Application.DTOs.Auth;
using FoodLoop.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.API.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>POST /auth/register — creates a new user account.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, ClientIp, cancellationToken);
        if (!result.Success)
            return BadRequest(ApiResponse.Fail(result.Message ?? "Registration failed.", result.Errors));

        return CreatedAtAction(nameof(Register), ApiResponse<AuthResponse>.Ok(result.Data!));
    }

    /// <summary>POST /auth/login — returns access and refresh tokens.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, ClientIp, cancellationToken);
        if (!result.Success)
            return Unauthorized(ApiResponse.Fail(result.Message ?? "Invalid credentials."));

        return Ok(ApiResponse<AuthResponse>.Ok(result.Data!));
    }

    /// <summary>POST /auth/refresh — returns a new access token (rotates the refresh token).</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken, ClientIp, cancellationToken);
        if (!result.Success)
            return Unauthorized(ApiResponse.Fail(result.Message ?? "Invalid refresh token."));

        return Ok(ApiResponse<AuthResponse>.Ok(result.Data!));
    }

    /// <summary>POST /auth/logout — invalidates the current session's refresh token.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return Ok(ApiResponse.Ok("Logged out."));
    }

    /// <summary>POST /auth/forgot-password — sends a password reset email.</summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.ForgotPasswordAsync(request.Email, cancellationToken);
        // Always 200, regardless of whether the email exists, to avoid account enumeration.
        return Ok(ApiResponse.Ok("If that email is registered, a reset link has been sent."));
    }

    /// <summary>POST /auth/reset-password — updates the user's password using a reset token.</summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.ResetPasswordAsync(request, cancellationToken);
        if (!result.Success)
            return BadRequest(ApiResponse.Fail(result.Message ?? "Unable to reset password.", result.Errors));

        return Ok(ApiResponse.Ok("Password has been reset."));
    }
}
