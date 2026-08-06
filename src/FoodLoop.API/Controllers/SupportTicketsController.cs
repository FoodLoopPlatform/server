using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.SupportTickets.Commands;
using FoodLoop.Application.Features.SupportTickets.Queries;
using FoodLoop.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.API.Controllers;

[ApiController]
[Route("support-tickets")]
[Authorize]
public class SupportTicketsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public SupportTicketsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    private Guid UserId => _currentUser.UserId ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// POST /support-tickets — open a support ticket.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateTicket([FromBody] CustomerCreateTicketRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<TicketPriority>(request.Priority, true, out var priority))
        {
            priority = TicketPriority.Low;
        }

        var command = new CreateSupportTicketCommand(UserId, request.Category, request.Message, priority);
        var ticket = await _mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<SupportTicketDto>.Ok(ticket));
    }

    /// <summary>
    /// GET /support-tickets — list caller's support tickets.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyTickets(CancellationToken cancellationToken)
    {
        var query = new GetCustomerSupportTicketsQuery(UserId);
        var tickets = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SupportTicketDto>>.Ok(tickets));
    }

    /// <summary>
    /// GET /support-tickets/{id} — get ticket detail and messages.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTicketDetail(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetCustomerSupportTicketDetailQuery(id, UserId);
        var ticket = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<SupportTicketDetailDto>.Ok(ticket));
    }

    /// <summary>
    /// POST /support-tickets/{id}/reply — reply to support ticket conversation.
    /// </summary>
    [HttpPost("{id:guid}/reply")]
    public async Task<IActionResult> Reply(Guid id, [FromBody] CustomerReplyTicketRequest request, CancellationToken cancellationToken)
    {
        var command = new CustomerReplyToSupportTicketCommand(UserId, id, request.Message);
        var result = await _mediator.Send(command, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(ApiResponse.Fail(result.Message ?? "Failed to reply to ticket"));
        }
        return Ok(ApiResponse<TicketMessageDto>.Ok(result.Data!));
    }
}

public class CustomerCreateTicketRequest
{
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Priority { get; set; } = "Low";
}

public class CustomerReplyTicketRequest
{
    public string Message { get; set; } = string.Empty;
}
