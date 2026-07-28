using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Features.Admin;

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

        // 2. Store documents verified (if merchant/charity)
        var store = await _db.Stores.FirstOrDefaultAsync(
            s => s.OwnerId == request.UserId && !s.IsDeleted, cancellationToken);

        if (store != null)
        {
            var verifications = await _db.StoreVerifications
                .Where(v => v.StoreId == store.Id && v.ReviewedAt != null)
                .OrderByDescending(v => v.ReviewedAt)
                .Take(5)
                .ToListAsync(cancellationToken);

            foreach (var v in verifications)
            {
                entries.Add(new ActivityLogEntryDto
                {
                    EventType = "DocumentVerified",
                    Title = "Document Reviewed",
                    Description = $"{v.VerificationType} was marked {v.Status} by admin.",
                    OccurredAt = v.ReviewedAt!.Value,
                });
            }
        }

        // 3. Recent orders
        var orders = await _db.Orders
            .Where(o => o.UserId == request.UserId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
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

        // 4. Support tickets
        var tickets = await _db.SupportTickets
            .Where(t => t.UserId == request.UserId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(3)
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
