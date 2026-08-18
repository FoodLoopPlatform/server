using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin.Queries;

public class GetStoreCommissionsQueryHandler : IRequestHandler<GetStoreCommissionsQuery, IReadOnlyList<StoreCommissionDto>>
{
    private readonly ApplicationDbContext _db;

    public GetStoreCommissionsQueryHandler(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<StoreCommissionDto>> Handle(GetStoreCommissionsQuery request, CancellationToken cancellationToken)
    {
        // 1. Fetch system settings for the global platform commission percentage
        var settings = await _db.SystemSettings
            .AsNoTracking()
            .FirstAsync(s => s.Id == SystemSettings.SingletonId, cancellationToken);
        var commissionPercent = settings.PlatformCommissionPercent;

        // 2. Fetch all active organizations (stores) along with their owner's email
        var storeInfos = await _db.Organizations
            .Where(o => !o.IsDeleted)
            .Join(_db.Users,
                  o => o.OwnerId,
                  u => u.Id,
                  (o, u) => new { Store = o, OwnerEmail = u.Email ?? string.Empty })
            .ToListAsync(cancellationToken);

        // 3. Fetch total sales grouped by store for completed/paid orders, ignoring soft-deleted product filters
        var salesByStore = await _db.OrderItems
            .IgnoreQueryFilters()
            .Where(oi => oi.Order!.OrderStatus == OrderStatus.Completed || oi.Order.PaymentStatus == PaymentStatus.Paid)
            .GroupBy(oi => oi.Product!.OrganizationId)
            .Select(g => new
            {
                StoreId = g.Key,
                TotalSales = g.Sum(oi => oi.UnitPrice * oi.Quantity)
            })
            .ToDictionaryAsync(x => x.StoreId, x => x.TotalSales, cancellationToken);

        // 4. Map to DTOs
        var result = storeInfos.Select(info =>
        {
            var storeId = info.Store.Id;
            var totalSales = salesByStore.TryGetValue(storeId, out var sales) ? sales : 0.00m;
            var totalCommissionGenerated = totalSales * (commissionPercent / 100.0m);
            var commissionWithdrawn = info.Store.CommissionWithdrawn;
            var outstandingCommission = totalCommissionGenerated - commissionWithdrawn;

            return new StoreCommissionDto
            {
                StoreId = storeId,
                StoreName = info.Store.Name,
                OwnerEmail = info.OwnerEmail,
                PlatformCommissionPercent = commissionPercent,
                TotalSales = totalSales,
                TotalCommissionGenerated = totalCommissionGenerated,
                CommissionWithdrawn = commissionWithdrawn,
                OutstandingCommission = outstandingCommission
            };
        }).ToList();

        return result;
    }
}
