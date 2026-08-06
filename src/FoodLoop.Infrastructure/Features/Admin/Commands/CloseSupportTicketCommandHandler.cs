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

    public CloseSupportTicketCommandHandler(ApplicationDbContext context)
    {
        _context = context;
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
    }
}

