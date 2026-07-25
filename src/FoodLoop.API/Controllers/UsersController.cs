using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.Users.Commands;
using FoodLoop.Application.Features.Users.Queries;
using FoodLoop.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.API.Controllers;

[ApiController]
[Route("users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICurrentUserService _currentUser;

    public UsersController(ISender mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    private Guid UserId => _currentUser.UserId ?? throw new UnauthorizedAccessException();

    /// <summary>GET /users/me — returns authenticated user information.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var user = await _mediator.Send(new GetCurrentUserQuery(UserId), cancellationToken);
        return Ok(ApiResponse<UserDto>.Ok(user));
    }

    /// <summary>PATCH /users/me — updates profile information (name, picture, language).</summary>
    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await _mediator.Send(new UpdateProfileCommand(UserId, request), cancellationToken);
        return Ok(ApiResponse<UserDto>.Ok(user));
    }

    /// <summary>PATCH /users/me/preferences — updates notification and application settings.</summary>
    [HttpPatch("me/preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesRequest request, CancellationToken cancellationToken)
    {
        await _mediator.Send(new UpdatePreferencesCommand(UserId, request), cancellationToken);
        return Ok(ApiResponse.Ok("Preferences updated."));
    }

    /// <summary>GET /users/me/addresses</summary>
    [HttpGet("me/addresses")]
    public async Task<IActionResult> GetAddresses(CancellationToken cancellationToken)
    {
        var addresses = await _mediator.Send(new GetAddressesQuery(UserId), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AddressDto>>.Ok(addresses));
    }

    /// <summary>POST /users/me/addresses</summary>
    [HttpPost("me/addresses")]
    public async Task<IActionResult> CreateAddress([FromBody] CreateAddressRequest request, CancellationToken cancellationToken)
    {
        var address = await _mediator.Send(new CreateAddressCommand(UserId, request), cancellationToken);
        return CreatedAtAction(nameof(GetAddresses), ApiResponse<AddressDto>.Ok(address));
    }

    /// <summary>PATCH /users/me/addresses/{id}</summary>
    [HttpPatch("me/addresses/{id:guid}")]
    public async Task<IActionResult> UpdateAddress(Guid id, [FromBody] UpdateAddressRequest request, CancellationToken cancellationToken)
    {
        var address = await _mediator.Send(new UpdateAddressCommand(UserId, id, request), cancellationToken);
        return Ok(ApiResponse<AddressDto>.Ok(address));
    }

    /// <summary>DELETE /users/me/addresses/{id}</summary>
    [HttpDelete("me/addresses/{id:guid}")]
    public async Task<IActionResult> DeleteAddress(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteAddressCommand(UserId, id), cancellationToken);
        return NoContent();
    }

    /// <summary>GET /users — lists all users with optional filtering (Admin only).</summary>
    [HttpGet]
    [Authorize(Roles = AppRole.Administrator)]
    public async Task<IActionResult> ListUsers(
        [FromQuery] string? role,
        [FromQuery] string? status,
        [FromQuery] string? searchTerm,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var users = await _mediator.Send(new ListUsersQuery(role, status, searchTerm, page, pageSize), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<UserDto>>.Ok(users));
    }

    /// <summary>GET /users/{id} — returns specific user details (Admin only).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = AppRole.Administrator)]
    public async Task<IActionResult> GetUserById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _mediator.Send(new GetUserByIdQuery(id), cancellationToken);
        return Ok(ApiResponse<UserDto>.Ok(user));
    }

    /// <summary>POST /users — creates a user directly (Admin only).</summary>
    [HttpPost]
    [Authorize(Roles = AppRole.Administrator)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateUserCommand(request), cancellationToken);
        if (!result.Success)
            return BadRequest(ApiResponse.Fail(result.Message ?? "Failed to create user.", result.Errors));

        return CreatedAtAction(nameof(GetUserById), new { id = result.Data!.Id }, ApiResponse<UserDto>.Ok(result.Data));
    }

    /// <summary>PATCH /users/{id} — updates user details (Admin only).</summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Roles = AppRole.Administrator)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateUserCommand(id, request), cancellationToken);
        if (!result.Success)
            return BadRequest(ApiResponse.Fail(result.Message ?? "Failed to update user.", result.Errors));

        return Ok(ApiResponse<UserDto>.Ok(result.Data!));
    }

    /// <summary>DELETE /users/{id} — removes user account (Admin only).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AppRole.Administrator)]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteUserCommand(id), cancellationToken);
        if (!result.Success)
            return BadRequest(ApiResponse.Fail(result.Message ?? "Failed to delete user.", result.Errors));

        return NoContent();
    }
}
