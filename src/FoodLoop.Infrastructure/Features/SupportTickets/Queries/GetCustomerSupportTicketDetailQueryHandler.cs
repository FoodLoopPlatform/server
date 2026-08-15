using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.SupportTickets.Queries;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.SupportTickets.Queries;

public class GetCustomerSupportTicketDetailQueryHandler : IRequestHandler<GetCustomerSupportTicketDetailQuery, SupportTicketDetailDto>
{
    private readonly ApplicationDbContext _db;

    public GetCustomerSupportTicketDetailQueryHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SupportTicketDetailDto> Handle(GetCustomerSupportTicketDetailQuery request, CancellationToken cancellationToken)
    {
        var ticket = await _db.SupportTickets
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new NotFoundException("SupportTicket", request.TicketId);

        if (ticket.UserId != request.UserId)
        {
            throw new UnauthorizedAccessException("You are not authorized to view this support ticket.");
        }

        var user = await _db.Users.FindAsync(new object[] { request.UserId }, cancellationToken);
        var name = user?.FullName ?? string.Empty;
        var email = user?.Email ?? string.Empty;

        var messageDtos = ticket.Messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new TicketMessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderName = m.SenderId == request.UserId ? name : "Support Agent",
                Message = m.Message,
                Attachment = m.Attachment,
                CreatedAt = m.CreatedAt
            })
            .ToList();

        return new SupportTicketDetailDto
        {
            Id = ticket.Id,
            UserId = ticket.UserId,
            UserEmail = email,
            UserFullName = name,
            Category = ticket.Category,
            Priority = ticket.Priority.ToString(),
            Status = ticket.Status.ToString(),
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            Messages = messageDtos
        };
    }
}
