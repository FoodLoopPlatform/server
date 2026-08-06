using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin.Commands;

public class ReplyToSupportTicketCommandHandler : IRequestHandler<ReplyToSupportTicketCommand, TicketMessageDto>
{
    private readonly ApplicationDbContext _context;

    public ReplyToSupportTicketCommandHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TicketMessageDto> Handle(ReplyToSupportTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _context.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket == null)
        {
            throw new NotFoundException("SupportTicket", request.TicketId);
        }

        var message = new TicketMessage
        {
            TicketId = request.TicketId,
            SenderId = request.SenderId,
            Message = request.Message,
            Attachment = request.Attachment,
            CreatedAt = System.DateTimeOffset.UtcNow
        };

        _context.TicketMessages.Add(message);
        
        ticket.UpdatedAt = System.DateTimeOffset.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);

        var senderName = await _context.Users
            .Where(u => u.Id == request.SenderId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "System/Support";

        return new TicketMessageDto
        {
            Id = message.Id,
            SenderId = message.SenderId,
            SenderName = senderName,
            Message = message.Message,
            Attachment = message.Attachment,
            CreatedAt = message.CreatedAt
        };
    }
}

