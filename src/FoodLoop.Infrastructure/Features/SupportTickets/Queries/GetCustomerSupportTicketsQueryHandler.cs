using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.SupportTickets.Queries;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.SupportTickets.Queries;

public class GetCustomerSupportTicketsQueryHandler : IRequestHandler<GetCustomerSupportTicketsQuery, IReadOnlyList<SupportTicketDto>>
{
    private readonly ApplicationDbContext _db;

    public GetCustomerSupportTicketsQueryHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SupportTicketDto>> Handle(GetCustomerSupportTicketsQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FindAsync(new object[] { request.UserId }, cancellationToken);
        var name = user?.FullName ?? string.Empty;
        var email = user?.Email ?? string.Empty;

        var tickets = await _db.SupportTickets
            .Where(t => t.UserId == request.UserId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return tickets.Select(t => new SupportTicketDto
        {
            Id = t.Id,
            UserId = t.UserId,
            UserEmail = email,
            UserFullName = name,
            Category = t.Category,
            Priority = t.Priority.ToString(),
            Status = t.Status.ToString(),
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        }).ToList();
    }
}
