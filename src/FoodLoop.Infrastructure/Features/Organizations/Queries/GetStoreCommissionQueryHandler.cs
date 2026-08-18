using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Organizations.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Organizations.Queries;

public class GetStoreCommissionQueryHandler : IRequestHandler<GetStoreCommissionQuery, StoreCommissionDto>
{
    private readonly ApplicationDbContext _db;

    public GetStoreCommissionQueryHandler(ApplicationDbContext db) => _db = db;

    public async Task<StoreCommissionDto> Handle(GetStoreCommissionQuery request, CancellationToken cancellationToken)
    {
        // 1. Fetch organization
        var store = await _db.Organizations
            .FirstOrDefaultAsync(o => o.OwnerId == request.OwnerId && !o.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Organization), request.OwnerId);

        // 2. Fetch platform commission percentage
        var settings = await _db.SystemSettings
            .AsNoTracking()
            .FirstAsync(s => s.Id == SystemSettings.SingletonId, cancellationToken);
        var commissionPercent = settings.PlatformCommissionPercent;

        // 3. Fetch owner's email
        var ownerEmail = await _db.Users
            .Where(u => u.Id == store.OwnerId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        // 4. Calculate store sales
        var totalSales = await _db.OrderItems
            .IgnoreQueryFilters()
            .Where(oi => oi.Product!.OrganizationId == store.Id && (oi.Order!.OrderStatus == OrderStatus.Completed || oi.Order.PaymentStatus == PaymentStatus.Paid))
            .SumAsync(oi => oi.UnitPrice * oi.Quantity, cancellationToken);

        var totalCommissionGenerated = totalSales * (commissionPercent / 100.0m);
        var commissionWithdrawn = store.CommissionWithdrawn;
        var outstandingCommission = totalCommissionGenerated - commissionWithdrawn;

        return new StoreCommissionDto
        {
            StoreId = store.Id,
            StoreName = store.Name,
            OwnerEmail = ownerEmail,
            PlatformCommissionPercent = commissionPercent,
            TotalSales = totalSales,
            TotalCommissionGenerated = totalCommissionGenerated,
            CommissionWithdrawn = commissionWithdrawn,
            OutstandingCommission = outstandingCommission
        };
    }
}
