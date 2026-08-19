using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Notifications;
using FoodLoop.Application.Features.Notifications.Commands;
using FoodLoop.Application.Features.Notifications.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.API.Controllers;

[ApiController]
[Route("notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public NotificationsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    private Guid UserId => _currentUser.UserId ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// GET /notifications — list caller's notifications with optional paging and filtering.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isRead = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetMyNotificationsQuery(UserId, pageNumber, pageSize, isRead);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<NotificationDto>>.Ok(result));
    }

    /// <summary>
    /// GET /notifications/unread-count — get unread notifications count.
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var query = new GetUnreadNotificationsCountQuery(UserId);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<int>.Ok(result));
    }

    /// <summary>
    /// PATCH /notifications/{id}/read — mark notification read and return navigation data.
    /// </summary>
    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var command = new MarkNotificationReadCommand(UserId, id);
        var result = await _mediator.Send(command, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(ApiResponse.Fail(result.Message ?? "Failed to mark read"));
        }
        return Ok(ApiResponse<NotificationDto>.Ok(result.Data!));
    }

    /// <summary>
    /// PATCH /notifications/read-all — mark all read.
    /// </summary>
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var command = new MarkAllNotificationsReadCommand(UserId);
        var result = await _mediator.Send(command, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(ApiResponse.Fail(result.Message ?? "Failed to mark all read"));
        }
        return NoContent();
    }

    /// <summary>
    /// POST /notifications/device-token — register a mobile device token for Firebase push notifications.
    /// </summary>
    [HttpPost("device-token")]
    public async Task<IActionResult> RegisterDeviceToken([FromBody] RegisterDeviceTokenRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(ApiResponse.Fail("Device token is required."));
        }

        await _mediator.Send(new RegisterDeviceTokenCommand(UserId, request.Token, request.Platform ?? "Mobile"), cancellationToken);
        return Ok(ApiResponse<object?>.Ok(new { success = true }));
    }

    public sealed class RegisterDeviceTokenRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        public string? Platform { get; set; }
    }
}
