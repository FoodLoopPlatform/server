using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Domain.Enums;
using MediatR;
using System;

namespace FoodLoop.Application.Features.SupportTickets.Commands;

public record CreateSupportTicketCommand(
    Guid UserId,
    string Category,
    string Message,
    TicketPriority Priority
) : IRequest<SupportTicketDto>;
