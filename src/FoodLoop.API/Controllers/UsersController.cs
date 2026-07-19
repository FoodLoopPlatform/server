using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.API.Controllers;

[ApiController]
[Route("users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ICurrentUserService _currentUser;

    public UsersController(IUserService userService, ICurrentUserService currentUser)
    {
        _userService = userService;
        _currentUser = currentUser;
    }

    private Guid UserId => _currentUser.UserId ?? throw new UnauthorizedAccessException();

    /// <summary>GET /users/me — returns authenticated user information.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var user = await _userService.GetCurrentUserAsync(UserId, cancellationToken);
        return Ok(ApiResponse<UserDto>.Ok(user));
    }

    /// <summary>PATCH /users/me — updates profile information (name, picture, language).</summary>
    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await _userService.UpdateProfileAsync(UserId, request, cancellationToken);
        return Ok(ApiResponse<UserDto>.Ok(user));
    }

    /// <summary>PATCH /users/me/preferences — updates notification and application settings.</summary>
    [HttpPatch("me/preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesRequest request, CancellationToken cancellationToken)
    {
        await _userService.UpdatePreferencesAsync(UserId, request, cancellationToken);
        return Ok(ApiResponse.Ok("Preferences updated."));
    }

    /// <summary>GET /users/me/addresses</summary>
    [HttpGet("me/addresses")]
    public async Task<IActionResult> GetAddresses(CancellationToken cancellationToken)
    {
        var addresses = await _userService.GetAddressesAsync(UserId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AddressDto>>.Ok(addresses));
    }

    /// <summary>POST /users/me/addresses</summary>
    [HttpPost("me/addresses")]
    public async Task<IActionResult> CreateAddress([FromBody] CreateAddressRequest request, CancellationToken cancellationToken)
    {
        var address = await _userService.CreateAddressAsync(UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetAddresses), ApiResponse<AddressDto>.Ok(address));
    }

    /// <summary>PATCH /users/me/addresses/{id}</summary>
    [HttpPatch("me/addresses/{id:guid}")]
    public async Task<IActionResult> UpdateAddress(Guid id, [FromBody] UpdateAddressRequest request, CancellationToken cancellationToken)
    {
        var address = await _userService.UpdateAddressAsync(UserId, id, request, cancellationToken);
        return Ok(ApiResponse<AddressDto>.Ok(address));
    }

    /// <summary>DELETE /users/me/addresses/{id}</summary>
    [HttpDelete("me/addresses/{id:guid}")]
    public async Task<IActionResult> DeleteAddress(Guid id, CancellationToken cancellationToken)
    {
        await _userService.DeleteAddressAsync(UserId, id, cancellationToken);
        return NoContent();
    }
}
