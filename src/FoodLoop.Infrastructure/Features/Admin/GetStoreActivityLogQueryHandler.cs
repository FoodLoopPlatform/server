using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Domain.Entities;
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

namespace FoodLoop.Infrastructure.Features.Admin;

public class GetStoreActivityLogQueryHandler
    : IRequestHandler<GetStoreActivityLogQuery, IReadOnlyList<ActivityLogEntryDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;

    public GetStoreActivityLogQueryHandler(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<IReadOnlyList<ActivityLogEntryDto>> Handle(
        GetStoreActivityLogQuery request, CancellationToken cancellationToken)
    {
        var store = await _db.Stores.FirstOrDefaultAsync(
            s => s.Id == request.StoreId && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Store), request.StoreId);

        var owner = await _userManager.FindByIdAsync(store.OwnerId.ToString());

        var entries = new List<ActivityLogEntryDto>();

        // 1. Account created (Merchant owner user creation)
        if (owner != null)
        {
            entries.Add(new ActivityLogEntryDto
            {
                EventType = "AccountCreated",
                Title = "Merchant Account Created",
                Description = $"New merchant account registered with email {owner.Email} for store '{store.Name}'.",
                OccurredAt = owner.CreatedAt,
            });
        }

        // 2. Document Uploads & Reviews
        var verifications = await _db.StoreVerifications
            .Where(v => v.StoreId == store.Id)
            .OrderByDescending(v => v.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var v in verifications)
        {
            entries.Add(new ActivityLogEntryDto
            {
                EventType = "DocumentUploaded",
                Title = "Document Uploaded",
                Description = $"Uploaded {v.VerificationType} document.",
                OccurredAt = v.CreatedAt,
            });

            if (v.ReviewedAt.HasValue)
            {
                entries.Add(new ActivityLogEntryDto
                {
                    EventType = "DocumentVerified",
                    Title = "Document Reviewed",
                    Description = $"{v.VerificationType} was marked {v.Status} by admin.",
                    OccurredAt = v.ReviewedAt.Value,
                });
            }
        }

        // 3. Product Listings
        var products = await _db.Products
            .IgnoreQueryFilters()
            .Where(p => p.StoreId == store.Id)
            .OrderByDescending(p => p.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var p in products)
        {
            entries.Add(new ActivityLogEntryDto
            {
                EventType = "ProductListed",
                Title = "Product Listed",
                Description = $"Listed new product '{p.Title}'.",
                OccurredAt = p.CreatedAt,
            });

            if (p.IsDeleted && p.DeletedAt.HasValue)
            {
                entries.Add(new ActivityLogEntryDto
                {
                    EventType = "ProductDeleted",
                    Title = "Product Removed",
                    Description = $"Removed product '{p.Title}'.",
                    OccurredAt = p.DeletedAt.Value,
                });
            }
        }

        // 4. Orders Received
        var sales = await _db.OrderItems
            .IgnoreQueryFilters()
            .Include(oi => oi.Order)
            .Include(oi => oi.Product)
            .Where(oi => oi.Product!.StoreId == store.Id)
            .OrderByDescending(oi => oi.Order!.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        var uniqueSales = sales
            .Where(oi => oi.Order != null)
            .GroupBy(oi => oi.OrderId)
            .Select(g => g.First())
            .ToList();

        foreach (var s in uniqueSales)
        {
            entries.Add(new ActivityLogEntryDto
            {
                EventType = "OrderReceived",
                Title = "Order Received",
                Description = $"Received Order #{s.OrderId.ToString()[..8].ToUpper()} containing '{s.Product?.Title}'.",
                OccurredAt = s.Order!.CreatedAt,
            });
        }

        // 5. Support tickets opened by owner
        if (owner != null)
        {
            var tickets = await _db.SupportTickets
                .Where(t => t.UserId == owner.Id)
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
        }

        return entries
            .OrderByDescending(e => e.OccurredAt)
            .Take(20)
            .ToList();
    }
}
