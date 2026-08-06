using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin.Queries;

public class GetUserActivityLogQueryHandler
    : IRequestHandler<GetUserActivityLogQuery, IReadOnlyList<ActivityLogEntryDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;

    public GetUserActivityLogQueryHandler(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<IReadOnlyList<ActivityLogEntryDto>> Handle(
        GetUserActivityLogQuery request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString())
            ?? throw new NotFoundException(nameof(ApplicationUser), request.UserId);

        var entries = new List<ActivityLogEntryDto>();

        // 1. Account created
        entries.Add(new ActivityLogEntryDto
        {
            EventType = "AccountCreated",
            Title = "Account Created",
            Description = $"New account registered with email {user.Email}.",
            OccurredAt = user.CreatedAt,
        });

        // 2. Profile updated
        if (user.UpdatedAt.HasValue && user.UpdatedAt.Value != user.CreatedAt)
        {
            entries.Add(new ActivityLogEntryDto
            {
                EventType = "ProfileUpdated",
                Title = "Profile Updated",
                Description = "Updated profile information details.",
                OccurredAt = user.UpdatedAt.Value,
            });
        }

        // 3. Orders Placed (by this customer)
        var orders = await _db.Orders
            .Where(o => o.UserId == request.UserId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var o in orders)
        {
            entries.Add(new ActivityLogEntryDto
            {
                EventType = "OrderPlaced",
                Title = "Order Placed",
                Description = $"Order #{o.Id.ToString()[..8].ToUpper()} — {o.OrderStatus}.",
                OccurredAt = o.CreatedAt,
            });
        }

        // 4. Support tickets opened
        var tickets = await _db.SupportTickets
            .Where(t => t.UserId == request.UserId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);

        foreach (var t in tickets)
        {
            entries.Add(new ActivityLogEntryDto
            {
                EventType = "SupportTicket",
                Title = "Support Ticket Opened",
                Description = $"Ticket: {t.Category} — {t.Status}.",
                OccurredAt = t.CreatedAt,
            });
        }

        return entries
            .OrderByDescending(e => e.OccurredAt)
            .Take(20)
            .ToList();
    }
}

