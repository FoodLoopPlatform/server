using FoodLoop.Application.DTOs.Admin;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Admin.Commands;

public record ReplyToSupportTicketCommand(
    Guid TicketId,
    Guid SenderId,
    string Message,
    string? Attachment = null) : IRequest<TicketMessageDto>;
