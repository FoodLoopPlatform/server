using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin;

public class GetSupportTicketsQueryHandler : IRequestHandler<GetSupportTicketsQuery, IReadOnlyList<SupportTicketDto>>
{
    private readonly ApplicationDbContext _context;

    public GetSupportTicketsQueryHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SupportTicketDto>> Handle(GetSupportTicketsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.SupportTickets.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<TicketStatus>(request.Status, true, out var status))
        {
            query = query.Where(t => t.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Priority) && Enum.TryParse<TicketPriority>(request.Priority, true, out var priority))
        {
            query = query.Where(t => t.Priority == priority);
        }

        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var userIds = tickets.Select(t => t.UserId).Distinct().ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u, cancellationToken);

        return tickets.Select(t => new SupportTicketDto
        {
            Id = t.Id,
            UserId = t.UserId,
            UserEmail = users.TryGetValue(t.UserId, out var u) ? u.Email ?? string.Empty : string.Empty,
            UserFullName = users.TryGetValue(t.UserId, out var usr) ? usr.FullName : "Unknown User",
            Category = t.Category,
            Priority = t.Priority.ToString(),
            Status = t.Status.ToString(),
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        }).ToList();
    }
}
