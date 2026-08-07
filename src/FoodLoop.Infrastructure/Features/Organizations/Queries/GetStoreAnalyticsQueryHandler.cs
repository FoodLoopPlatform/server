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

        var orders = await _db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Where(o => o.Items.Any(i => i.Product!.OrganizationId == org.Id))
            .ToListAsync(cancellationToken);

        var revenue = orders
            .Where(o => o.OrderStatus == OrderStatus.Completed || o.PaymentStatus == PaymentStatus.Paid)
            .SelectMany(o => o.Items)
            .Where(i => i.Product!.OrganizationId == org.Id)
            .Sum(i => i.Quantity * i.UnitPrice);

        var ordersCount = orders.Count;

        var savingsImpact = orders
            .Where(o => o.OrderStatus == OrderStatus.Completed || o.PaymentStatus == PaymentStatus.Paid)
            .SelectMany(o => o.Items)
            .Where(i => i.Product!.OrganizationId == org.Id)
            .Sum(i => i.Quantity * (i.Product!.OriginalPrice - i.UnitPrice));

        var topProducts = orders
            .Where(o => o.OrderStatus == OrderStatus.Completed || o.PaymentStatus == PaymentStatus.Paid)
            .SelectMany(o => o.Items)
            .Where(i => i.Product!.OrganizationId == org.Id)
            .GroupBy(i => new { i.ProductId, i.Product!.Title, i.Product!.TitleAr })
            .Select(g => new TopProductDto
            {
                Id = g.Key.ProductId,
                Title = g.Key.Title,
                TitleAr = g.Key.TitleAr,
                QuantitySold = g.Sum(x => x.Quantity),
                RevenueGenerated = g.Sum(x => x.Quantity * x.UnitPrice)
            })
            .OrderByDescending(tp => tp.QuantitySold)
            .Take(5)
            .ToList();

        return new StoreAnalyticsDto
        {
            Revenue = revenue,
            OrdersCount = ordersCount,
            SavingsImpact = savingsImpact,
            TopProducts = topProducts
        };
    }
}
