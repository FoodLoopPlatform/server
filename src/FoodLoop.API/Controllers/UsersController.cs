using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.SupportTickets.Commands;
using FoodLoop.Application.Features.Users.Commands;
using FoodLoop.Application.Features.Users.Queries;
using FoodLoop.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using FoodLoop.Application.Features.Admin.Queries;

namespace FoodLoop.API.Controllers;

[ApiController]
[Route("users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalizationService _loc;

    public UsersController(ISender mediator, ICurrentUserService currentUser, ILocalizationService loc)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _loc = loc;
    }

    private Guid UserId => _currentUser.UserId ?? throw new UnauthorizedAccessException();

    /// <summary>GET /users/me — returns authenticated user information.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var user = await _mediator.Send(new GetCurrentUserQuery(UserId), cancellationToken);
        return Ok(ApiResponse<UserDto>.Ok(user));
    }

    /// <summary>GET /users/me/notes — retrieve all notes sent to the current customer/user.</summary>
    [HttpGet("me/notes")]
    public async Task<IActionResult> GetMyNotes(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetMyNotesQuery(UserId, pageNumber, pageSize);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminNoteDto>>.Ok(result));
    }

    /// <summary>GET /users/me/wallet — retrieve current user's wallet balance and transactions.</summary>
    [HttpGet("me/wallet")]
    public async Task<IActionResult> GetMyWallet(CancellationToken cancellationToken)
    {
        var query = new GetUserWalletQuery(UserId);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<UserWalletDto>.Ok(result));
    }

    /// <summary>PATCH /users/me — updates profile information (name, picture, language).</summary>
    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await _mediator.Send(new UpdateProfileCommand(UserId, request), cancellationToken);
        return Ok(ApiResponse<UserDto>.Ok(user));
    }

    /// <summary>POST /users/me/tickets — opens a new support ticket.</summary>
    [HttpPost("me/tickets")]
    public async Task<IActionResult> CreateSupportTicket([FromBody] CreateSupportTicketRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateSupportTicketCommand(UserId, request.Category, request.Message, request.Priority), cancellationToken);
        return Ok(ApiResponse<SupportTicketDto>.Ok(result));
    }

    /// <summary>GET /users/me/reports — list product issue reports submitted by current user.</summary>
    [HttpGet("me/reports")]
    public async Task<IActionResult> GetMyReports(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool? isResolved = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetMyReportsQuery(UserId, pageNumber, pageSize, isResolved), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DisputeDto>>.Ok(result));
    }

    /// <summary>PATCH /users/me/preferences — updates notification and application settings.</summary>
    [HttpPatch("me/preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesRequest request, CancellationToken cancellationToken)
    {
        await _mediator.Send(new UpdatePreferencesCommand(UserId, request), cancellationToken);
        return Ok(ApiResponse.Ok(_loc["PreferencesUpdated"]));
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
    [Authorize(Roles = AppRole.Admin)]
    public async Task<IActionResult> ListUsers(
        [FromQuery] string? role,
        [FromQuery] string? status,
        [FromQuery] string? searchTerm,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListUsersQuery(role, status, searchTerm, page, pageSize), cancellationToken);
        return Ok(ApiResponse<PagedResult<UserDto>>.Ok(result));
    }

    /// <summary>GET /users/{id} — returns specific user details (Admin only).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = AppRole.Admin)]
    public async Task<IActionResult> GetUserById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _mediator.Send(new GetUserByIdQuery(id), cancellationToken);
        return Ok(ApiResponse<UserDto>.Ok(user));
    }

    /// <summary>POST /users — creates a user directly (Admin only).</summary>
    [HttpPost]
    [Authorize(Roles = AppRole.Admin)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateUserCommand(request), cancellationToken);
        if (!result.Success)
            return BadRequest(ApiResponse.Fail(result.Message ?? _loc["FailedToCreateUser"], result.Errors));

        return CreatedAtAction(nameof(GetUserById), new { id = result.Data!.Id }, ApiResponse<UserDto>.Ok(result.Data));
    }

    /// <summary>PATCH /users/{id} — updates user details (Admin only).</summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Roles = AppRole.Admin)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateUserCommand(id, request), cancellationToken);
        if (!result.Success)
            return BadRequest(ApiResponse.Fail(result.Message ?? _loc["FailedToUpdateUser"], result.Errors));

        return Ok(ApiResponse<UserDto>.Ok(result.Data!));
    }

    /// <summary>DELETE /users/{id} — removes user account (Admin only).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AppRole.Admin)]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteUserCommand(id), cancellationToken);
        if (!result.Success)
            return BadRequest(ApiResponse.Fail(result.Message ?? _loc["FailedToDeleteUser"], result.Errors));

        return NoContent();
    }
}
