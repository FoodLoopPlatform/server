using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Common.Models;
using MediatR;
using System;

namespace FoodLoop.Application.Features.SupportTickets.Commands;

public record CustomerReplyToSupportTicketCommand(
    Guid UserId,
    Guid TicketId,
    string Message
) : IRequest<Result<TicketMessageDto>>;
