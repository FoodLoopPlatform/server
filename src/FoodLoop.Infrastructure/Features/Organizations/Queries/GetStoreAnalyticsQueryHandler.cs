using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.Features.Organizations.Queries;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Organizations.Queries;

public class GetStoreAnalyticsQueryHandler : IRequestHandler<GetStoreAnalyticsQuery, StoreAnalyticsDto>
{
    private readonly ApplicationDbContext _db;

    public GetStoreAnalyticsQueryHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<StoreAnalyticsDto> Handle(GetStoreAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.OwnerId == request.OwnerId && !o.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Organization", request.OwnerId);

        // Determine the start of the requested period (UTC)
        DateTimeOffset? since = request.Period?.ToLowerInvariant() switch
        {
            "today" => DateTimeOffset.UtcNow.Date,
            "week"  => DateTimeOffset.UtcNow.Date.AddDays(-6),
            "month" => DateTimeOffset.UtcNow.Date.AddDays(-29),
            _       => null   // "all" or unrecognised — no date filter
        };

        var ordersQuery = _db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Where(o => o.Items.Any(i => i.Product!.OrganizationId == org.Id));

        if (since.HasValue)
            ordersQuery = ordersQuery.Where(o => o.CreatedAt >= since.Value);

        var orders = await ordersQuery.ToListAsync(cancellationToken);

        // Only completed / paid orders contribute to revenue and savings
        var completedItems = orders
            .Where(o => o.OrderStatus == OrderStatus.Completed || o.PaymentStatus == PaymentStatus.Paid)
            .SelectMany(o => o.Items)
            .Where(i => i.Product!.OrganizationId == org.Id)
            .ToList();

        var revenue      = completedItems.Sum(i => i.Quantity * i.UnitPrice);
        var ordersCount  = orders.Count(o => o.OrderStatus == OrderStatus.Completed || o.PaymentStatus == PaymentStatus.Paid);
        var savingsImpact = completedItems.Sum(i => i.Quantity * (i.Product!.OriginalPrice - i.UnitPrice));

        var topProducts = completedItems
            .GroupBy(i => new { i.ProductId, i.Product!.Title })
            .Select(g => new TopProductDto
            {
                Id = g.Key.ProductId,
                Title = g.Key.Title,
                QuantitySold = g.Sum(x => x.Quantity),
                RevenueGenerated = g.Sum(x => x.Quantity * x.UnitPrice)
            })
            .OrderByDescending(tp => tp.QuantitySold)
            .Take(5)
            .ToList();

        return new StoreAnalyticsDto
        {
            Period       = request.Period ?? "all",
            Revenue      = revenue,
            OrdersCount  = ordersCount,
            SavingsImpact = savingsImpact,
            TopProducts  = topProducts
        };
    }
}
