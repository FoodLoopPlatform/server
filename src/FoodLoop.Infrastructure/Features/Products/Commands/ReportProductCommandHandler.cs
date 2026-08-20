using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Products.Commands;

public class ReportProductCommandHandler : IRequestHandler<ReportProductCommand, Unit>
{
    private readonly ApplicationDbContext _db;
    private readonly FoodLoop.Application.Common.Interfaces.IAuditLogService _auditLogService;
    private readonly IRealTimeNotificationService? _notificationService;
    private readonly IFileStorageService? _fileStorage;

    public ReportProductCommandHandler(
        ApplicationDbContext db,
        FoodLoop.Application.Common.Interfaces.IAuditLogService auditLogService)
        : this(db, auditLogService, null!, null!)
    {
    }

    public ReportProductCommandHandler(
        ApplicationDbContext db,
        FoodLoop.Application.Common.Interfaces.IAuditLogService auditLogService,
        IRealTimeNotificationService notificationService)
        : this(db, auditLogService, notificationService, null!)
    {
    }

    public ReportProductCommandHandler(
        ApplicationDbContext db,
        FoodLoop.Application.Common.Interfaces.IAuditLogService auditLogService,
        IRealTimeNotificationService notificationService,
        IFileStorageService fileStorage)
    {
        _db = db;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
        _fileStorage = fileStorage;
    }

    public async Task<Unit> Handle(ReportProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        if (request.ImageFile == null || request.ImageFile.Content == null)
            throw new ArgumentException("Evidence image is required.");

        var finalImageUrl = _fileStorage != null 
            ? await _fileStorage.SaveAsync(request.ImageFile, $"reports/{request.ProductId}", cancellationToken)
            : null;

        if (finalImageUrl != null && finalImageUrl.Length > 500)
            throw new ArgumentException("ImageUrl must be 500 characters or fewer.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var reasonString = request.Reason.ToString();

        // 1. Anti-Sabotage: Fetch existing UNRESOLVED expired reports for this organization
        var existingActiveReports = await _db.ProductReports
            .Include(r => r.Product)
            .Where(r => r.Product != null && r.Product.OrganizationId == product.OrganizationId &&
                        !r.IsResolved &&
                        (r.Product.ExpirationDate < today || r.Reason == "Expired" || r.Reason == "WrongExpiry"))
            .ToListAsync(cancellationToken);

        var isCurrentReportExpired = product.ExpirationDate < today || request.Reason == ProductReportReason.Expired || request.Reason == ProductReportReason.WrongExpiry;

        // Distinct customer strikes calculation
        var distinctReporters = existingActiveReports.Select(r => r.ReportedBy).ToHashSet();
        if (isCurrentReportExpired)
        {
            distinctReporters.Add(request.ReportedBy);
        }
        var activeStrikes = isCurrentReportExpired ? distinctReporters.Count : 0;

        var report = new ProductReport
        {
            ProductId = request.ProductId,
            ReportedBy = request.ReportedBy,
            Reason = reasonString,
            Details = request.Details,
            ImageUrl = finalImageUrl
        };

        _db.ProductReports.Add(report);

        var settings = await _db.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == SystemSettings.SingletonId, cancellationToken);
        var threshold = settings?.MaxExpiredReportsBeforeDeactivation ?? 3;

        var organization = await _db.Organizations
            .FirstOrDefaultAsync(o => o.Id == product.OrganizationId, cancellationToken)
            ?? throw new NotFoundException("Organization", product.OrganizationId);

        var isAutoSuspended = false;

        if (isCurrentReportExpired && activeStrikes >= threshold)
        {
            organization.VerificationStatus = VerificationStatus.Rejected;
            var notice = $"\n[{DateTimeOffset.UtcNow:u}] Auto-deactivated: Exceeded maximum allowed unresolved expired product reports ({activeStrikes}/{threshold} distinct customer strikes).";
            organization.AdminNote = (organization.AdminNote ?? "") + notice;
            organization.UpdatedAt = DateTimeOffset.UtcNow;

            var owner = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == organization.OwnerId, cancellationToken);
            if (owner != null)
            {
                owner.Status = UserStatus.Suspended;
            }
            isAutoSuspended = true;
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            request.ReportedBy,
            product.OrganizationId,
            "ProductReported",
            "Product Reported by Customer",
            $"Customer reported product '{product.Title}'. Reason: {request.Reason}.",
            null,
            cancellationToken);

        if (_notificationService != null)
        {
            // Always notify Admin role of incoming reports
            await _notificationService.SendNotificationToRoleAsync(
                "Admin",
                "NotifProductReportedTitle",
                "NotifProductReportedBody",
                "ProductReported",
                new object[] { product.Title, reasonString },
                "ProductReport",
                report.Id,
                cancellationToken);

            if (isAutoSuspended)
            {
                // Send Store Owner Suspension Alert
                await _notificationService.SendNotificationToUserAsync(
                    organization.OwnerId,
                    "NotifStoreSuspendedTitle",
                    "NotifStoreSuspendedBody",
                    "StoreSuspended",
                    new object[] { activeStrikes, threshold },
                    "Organization",
                    organization.Id,
                    cancellationToken);

                // Send Urgent Auto-Suspension Notice to Admin Role
                await _notificationService.SendNotificationToRoleAsync(
                    "Admin",
                    "NotifStoreAutoSuspendedTitle",
                    "NotifStoreAutoSuspendedBody",
                    "StoreSuspended",
                    new object[] { organization.Name, activeStrikes, threshold },
                    "Organization",
                    organization.Id,
                    cancellationToken);
            }
            else if (isCurrentReportExpired)
            {
                if (activeStrikes == threshold - 1)
                {
                    // Urgent Warning (Strike 2 of 3)
                    await _notificationService.SendNotificationToUserAsync(
                        organization.OwnerId,
                        "NotifDisputeUrgentWarningTitle",
                        "NotifDisputeUrgentWarningBody",
                        "DisputeUrgentWarning",
                        new object[] { activeStrikes, product.Title, threshold },
                        "ProductReport",
                        report.Id,
                        cancellationToken);
                }
                else
                {
                    // Initial Warning (Strike 1 of 3)
                    await _notificationService.SendNotificationToUserAsync(
                        organization.OwnerId,
                        "NotifDisputeWarningTitle",
                        "NotifDisputeWarningBody",
                        "DisputeWarning",
                        new object[] { product.Title, reasonString, activeStrikes, threshold },
                        "ProductReport",
                        report.Id,
                        cancellationToken);
                }
            }
        }

        return Unit.Value;
    }
}
