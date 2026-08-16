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

        // 1. Fetch Orders for store
        var ordersQuery = _db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Where(o => o.Items.Any(i => i.Product!.OrganizationId == org.Id));

        if (since.HasValue)
            ordersQuery = ordersQuery.Where(o => o.CreatedAt >= since.Value);

        var orders = await ordersQuery.ToListAsync(cancellationToken);

        // Only completed / paid orders contribute to revenue and savings
        var completedOrders = orders
            .Where(o => o.OrderStatus == OrderStatus.Completed || o.PaymentStatus == PaymentStatus.Paid)
            .ToList();

        var completedItems = completedOrders
            .SelectMany(o => o.Items)
            .Where(i => i.Product!.OrganizationId == org.Id)
            .ToList();

        var revenue = completedItems.Sum(i => i.Quantity * i.UnitPrice);
        var ordersCount = completedOrders.Count;
        var savingsImpact = completedItems.Sum(i => i.Quantity * (i.Product!.OriginalPrice - i.UnitPrice));

        var averageOrderValue = ordersCount > 0 ? revenue / ordersCount : 0.00m;

        // 2. Fetch Refunds for store orders in this period
        var orderIds = orders.Select(o => o.Id.ToString()).ToList();
        var refundQuery = _db.WalletTransactions
            .AsNoTracking()
            .Where(t => t.Type == "Refund" && t.ReferenceId != null && orderIds.Contains(t.ReferenceId));

        if (since.HasValue)
            refundQuery = refundQuery.Where(t => t.CreatedAt >= since.Value);

        var refundedAmount = await refundQuery.SumAsync(t => t.Amount, cancellationToken);

        // 3. Fetch Donations for store in this period
        var donationsQuery = _db.Donations
            .Include(d => d.Product)
            .Where(d => d.DonorOrganizationId == org.Id);

        if (since.HasValue)
            donationsQuery = donationsQuery.Where(d => d.CreatedAt >= since.Value);

        var donations = await donationsQuery.ToListAsync(cancellationToken);
        var donatedValue = donations.Sum(d => d.Quantity * (d.Product?.DiscountedPrice ?? d.Product?.OriginalPrice ?? 0.00m));

        // 4. Order status breakdown
        var pendingCount = orders.Count(o => o.OrderStatus == OrderStatus.Pending);
        var confirmedCount = orders.Count(o => o.OrderStatus == OrderStatus.Confirmed);
        var preparingCount = orders.Count(o => o.OrderStatus == OrderStatus.Preparing);
        var readyForPickupCount = orders.Count(o => o.OrderStatus == OrderStatus.ReadyForPickup);
        var completedCount = orders.Count(o => o.OrderStatus == OrderStatus.Completed);
        var cancelledCount = orders.Count(o => o.OrderStatus == OrderStatus.Cancelled);

        // 5. Products snapshot (always current snapshot)
        var totalProducts = await _db.Products.CountAsync(p => p.OrganizationId == org.Id && !p.IsDeleted, cancellationToken);
        var outOfStockProducts = await _db.Products.CountAsync(p => p.OrganizationId == org.Id && !p.IsDeleted && p.QuantityAvailable == 0, cancellationToken);
        
        var todayDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var threeDaysFromNow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        var expiringSoonProducts = await _db.Products.CountAsync(
            p => p.OrganizationId == org.Id && !p.IsDeleted && p.ExpirationDate >= todayDate && p.ExpirationDate <= threeDaysFromNow, 
            cancellationToken);

        // 6. Disputes
        var disputesQuery = _db.ProductReports
            .Include(r => r.Product)
            .Where(r => r.Product!.OrganizationId == org.Id);

        if (since.HasValue)
            disputesQuery = disputesQuery.Where(r => r.CreatedAt >= since.Value);

        var disputes = await disputesQuery.ToListAsync(cancellationToken);
        var totalDisputes = disputes.Count;
        var unresolvedDisputes = disputes.Count(r => !r.IsResolved);
        var resolvedDisputes = disputes.Count(r => r.IsResolved);

        // 7. Top products
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
            Period = request.Period ?? "all",
            Revenue = revenue,
            OrdersCount = ordersCount,
            SavingsImpact = savingsImpact,
            
            AverageOrderValue = averageOrderValue,
            RefundedAmount = refundedAmount,
            DonatedValue = donatedValue,

            PendingOrdersCount = pendingCount,
            ConfirmedOrdersCount = confirmedCount,
            PreparingOrdersCount = preparingCount,
            ReadyForPickupOrdersCount = readyForPickupCount,
            CompletedOrdersCount = completedCount,
            CancelledOrdersCount = cancelledCount,

            TotalProductsCount = totalProducts,
            OutOfStockProductsCount = outOfStockProducts,
            ExpiringSoonProductsCount = expiringSoonProducts,

            TotalDisputesCount = totalDisputes,
            UnresolvedDisputesCount = unresolvedDisputes,
            ResolvedDisputesCount = resolvedDisputes,

            TopProducts = topProducts
        };
    }
}
