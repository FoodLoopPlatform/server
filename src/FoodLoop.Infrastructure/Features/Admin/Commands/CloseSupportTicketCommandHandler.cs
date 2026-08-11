using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin.Commands;

public class CloseSupportTicketCommandHandler : IRequestHandler<CloseSupportTicketCommand>
{
    private readonly ApplicationDbContext _context;
    private readonly FoodLoop.Application.Common.Interfaces.IAuditLogService _auditLogService;

    public CloseSupportTicketCommandHandler(ApplicationDbContext context, FoodLoop.Application.Common.Interfaces.IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    public async Task Handle(CloseSupportTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _context.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (ticket == null)
        {
            throw new NotFoundException("SupportTicket", request.Id);
        }

        ticket.Status = TicketStatus.Closed;
        ticket.UpdatedAt = System.DateTimeOffset.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            ticket.UserId,
            null,
            "SupportTicketClosed",
            "Support Ticket Closed",
            $"Administrator closed support ticket category '{ticket.Category}'.",
            null,
            cancellationToken);
    }
}

