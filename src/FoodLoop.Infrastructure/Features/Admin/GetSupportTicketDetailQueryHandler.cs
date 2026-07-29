using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin;

public class GetSupportTicketDetailQueryHandler : IRequestHandler<GetSupportTicketDetailQuery, SupportTicketDetailDto>
{
    private readonly ApplicationDbContext _context;

    public GetSupportTicketDetailQueryHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SupportTicketDetailDto> Handle(GetSupportTicketDetailQuery request, CancellationToken cancellationToken)
    {
        var ticket = await _context.SupportTickets
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (ticket == null)
        {
            throw new NotFoundException("SupportTicket", request.Id);
        }

        var userIds = ticket.Messages.Select(m => m.SenderId).Append(ticket.UserId).Distinct().ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u, cancellationToken);

        var ticketOwner = users.TryGetValue(ticket.UserId, out var to) ? to : null;

        var messageDtos = ticket.Messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new TicketMessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderName = users.TryGetValue(m.SenderId, out var s) ? s.FullName : "System/Support",
                Message = m.Message,
                Attachment = m.Attachment,
                CreatedAt = m.CreatedAt
            })
            .ToList();

        return new SupportTicketDetailDto
        {
            Id = ticket.Id,
            UserId = ticket.UserId,
            UserEmail = ticketOwner?.Email ?? string.Empty,
            UserFullName = ticketOwner?.FullName ?? "Unknown User",
            Category = ticket.Category,
            Priority = ticket.Priority.ToString(),
            Status = ticket.Status.ToString(),
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            Messages = messageDtos
        };
    }
}
