using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.SupportTickets.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.SupportTickets.Commands;

public class CustomerReplyToSupportTicketCommandHandler : IRequestHandler<CustomerReplyToSupportTicketCommand, Result<TicketMessageDto>>
{
    private readonly ApplicationDbContext _db;

    public CustomerReplyToSupportTicketCommandHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<TicketMessageDto>> Handle(CustomerReplyToSupportTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _db.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new NotFoundException("SupportTicket", request.TicketId);

        if (ticket.UserId != request.UserId)
        {
            return Result<TicketMessageDto>.Fail("Unauthorized to reply to this ticket.");
        }

        if (ticket.Status == TicketStatus.Resolved || ticket.Status == TicketStatus.Closed)
        {
            return Result<TicketMessageDto>.Fail("Cannot reply to a closed or resolved ticket.");
        }

        var message = new TicketMessage
        {
            TicketId = request.TicketId,
            SenderId = request.UserId,
            Message = request.Message
        };

        _db.TicketMessages.Add(message);
        ticket.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var user = await _db.Users.FindAsync(new object[] { request.UserId }, cancellationToken);

        return Result<TicketMessageDto>.Ok(new TicketMessageDto
        {
            Id = message.Id,
            SenderId = message.SenderId,
            SenderName = user?.FullName ?? "Unknown",
            Message = message.Message,
            Attachment = message.Attachment,
            CreatedAt = message.CreatedAt
        });
    }
}
