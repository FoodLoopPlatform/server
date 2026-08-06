using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.SupportTickets.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.SupportTickets;

public class CreateSupportTicketCommandHandler
    : IRequestHandler<CreateSupportTicketCommand, SupportTicketDto>
{
    private readonly ApplicationDbContext _db;

    public CreateSupportTicketCommandHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SupportTicketDto> Handle(CreateSupportTicketCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException("User", request.UserId);

        var ticket = new SupportTicket
        {
            UserId = request.UserId,
            Category = request.Category,
            Priority = request.Priority,
            Status = TicketStatus.Open
        };

        var initialMessage = new TicketMessage
        {
            TicketId = ticket.Id,
            SenderId = request.UserId,
            Message = request.Message
        };

        ticket.Messages.Add(initialMessage);

        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync(cancellationToken);

        return new SupportTicketDto
        {
            Id = ticket.Id,
            UserId = ticket.UserId,
            UserEmail = user.Email ?? string.Empty,
            UserFullName = user.FullName,
            Category = ticket.Category,
            Priority = ticket.Priority.ToString(),
            Status = ticket.Status.ToString(),
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt
        };
    }
}
