using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin.Commands;

public class WithdrawStoreCommissionCommandHandler
    : IRequestHandler<WithdrawStoreCommissionCommand, StoreCommissionDto>
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public WithdrawStoreCommissionCommandHandler(
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _db = db;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<StoreCommissionDto> Handle(WithdrawStoreCommissionCommand request, CancellationToken cancellationToken)
    {
        // 1. Fetch store
        var store = await _db.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.StoreId && !o.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Organization), request.StoreId);

        // 2. Validate amount
        if (request.Amount <= 0)
        {
            throw new ArgumentException("Amount to withdraw must be greater than zero.");
        }

        // 3. Fetch platform commission percentage
        var settings = await _db.SystemSettings
            .AsNoTracking()
            .FirstAsync(s => s.Id == SystemSettings.SingletonId, cancellationToken);
        var commissionPercent = settings.PlatformCommissionPercent;

        // 4. Calculate current outstanding commission
        var totalSales = await _db.OrderItems
            .IgnoreQueryFilters()
            .Where(oi => oi.Product!.OrganizationId == request.StoreId && (oi.Order!.OrderStatus == OrderStatus.Completed || oi.Order.PaymentStatus == PaymentStatus.Paid))
            .SumAsync(oi => oi.UnitPrice * oi.Quantity, cancellationToken);

        var totalCommissionGenerated = totalSales * (commissionPercent / 100.0m);
        var outstandingCommission = totalCommissionGenerated - store.CommissionWithdrawn;

        // 5. Validate that we don't withdraw more than outstanding commission
        if (request.Amount > outstandingCommission)
        {
            throw new ArgumentException($"Cannot withdraw {request.Amount} as it exceeds the outstanding commission of {outstandingCommission}.");
        }

        // 6. Update withdrawn commission
        store.CommissionWithdrawn += request.Amount;
        store.UpdatedAt = DateTimeOffset.UtcNow;
        _db.Organizations.Update(store);
        await _db.SaveChangesAsync(cancellationToken);

        // 7. Log audit log
        var adminId = _currentUserService.UserId;
        await _auditLogService.LogAsync(
            adminId,
            store.Id,
            "CommissionWithdrawn",
            "Commission Withdrawn",
            $"Withdrew platform commission of {request.Amount} from store '{store.Name}'.",
            null,
            cancellationToken);

        // 8. Fetch owner's email for the DTO
        var ownerEmail = await _db.Users
            .Where(u => u.Id == store.OwnerId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return new StoreCommissionDto
        {
            StoreId = store.Id,
            StoreName = store.Name,
            OwnerEmail = ownerEmail,
            PlatformCommissionPercent = commissionPercent,
            TotalSales = totalSales,
            TotalCommissionGenerated = totalCommissionGenerated,
            CommissionWithdrawn = store.CommissionWithdrawn,
            OutstandingCommission = totalCommissionGenerated - store.CommissionWithdrawn
        };
    }
}
