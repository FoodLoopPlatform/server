using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin.Commands;

public class ResolveDisputeCommandHandler : IRequestHandler<ResolveDisputeCommand, DisputeDto>
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditLogService _auditLogService;
    private readonly IRealTimeNotificationService? _notificationService;

    public ResolveDisputeCommandHandler(ApplicationDbContext db, IAuditLogService auditLogService)
        : this(db, auditLogService, null!)
    {
    }

    public ResolveDisputeCommandHandler(
        ApplicationDbContext db,
        IAuditLogService auditLogService,
        IRealTimeNotificationService notificationService)
    {
        _db = db;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
    }

    public async Task<DisputeDto> Handle(ResolveDisputeCommand request, CancellationToken cancellationToken)
    {
        var report = await _db.ProductReports
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == request.DisputeId, cancellationToken)
            ?? throw new NotFoundException("Dispute", request.DisputeId);

        report.IsResolved = true;
        report.AdminNote = request.AdminNote;
        report.ResolvedAt = DateTimeOffset.UtcNow;
        _db.ProductReports.Update(report);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            request.AdminId,
            report.Product?.OrganizationId,
            "DisputeResolved",
            "Product Dispute Resolved",
            $"Admin resolved dispute for product '{report.Product?.Title}'. Resolution note: {request.AdminNote}",
            null,
            cancellationToken);

        // Recalculate remaining active strikes if this belonged to an organization
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var orgId = report.Product?.OrganizationId;
        var remainingActiveStrikes = 0;

        if (orgId.HasValue)
        {
            remainingActiveStrikes = await _db.ProductReports
                .Include(r => r.Product)
                .Where(r => r.Product != null && r.Product.OrganizationId == orgId.Value &&
                            !r.IsResolved &&
                            (r.Product.ExpirationDate < today || r.Reason == "Expired" || r.Reason == "WrongExpiry"))
                .Select(r => r.ReportedBy)
                .Distinct()
                .CountAsync(cancellationToken);
        }

        var settings = await _db.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == SystemSettings.SingletonId, cancellationToken);
        var threshold = settings?.MaxExpiredReportsBeforeDeactivation ?? 5;

        if (_notificationService != null)
        {
            // Notify Customer of Admin Decision
            await _notificationService.SendNotificationToUserAsync(
                report.ReportedBy,
                "NotifDisputeResolvedByAdminCustomerTitle",
                "NotifDisputeResolvedByAdminCustomerBody",
                "DisputeResolvedByAdmin",
                new object[] { report.Product?.Title ?? "Product", request.AdminNote },
                "ProductReport",
                report.Id,
                cancellationToken);

            // If store owner exists, notify store owner of Admin Decision and remaining strikes
            if (orgId.HasValue)
            {
                var org = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgId.Value, cancellationToken);
                if (org != null)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        org.OwnerId,
                        "NotifDisputeResolvedByAdminMerchantTitle",
                        "NotifDisputeResolvedByAdminMerchantBody",
                        "DisputeResolvedByAdmin",
                        new object[] { report.Product?.Title ?? "Product", request.AdminNote, remainingActiveStrikes, threshold },
                        "ProductReport",
                        report.Id,
                        cancellationToken);
                }
            }
        }

        // Reload reporter name
        var reporter = await _db.Users.FindAsync(new object[] { report.ReportedBy }, cancellationToken);

        return new DisputeDto
        {
            Id = report.Id,
            ProductId = report.ProductId,
            ProductTitle = report.Product?.Title ?? "Unknown",
            ReportedBy = report.ReportedBy,
            ReporterName = reporter?.FullName ?? "User",
            Reason = report.Reason,
            Details = report.Details,
            IsResolved = report.IsResolved,
            AdminNote = report.AdminNote,
            ResolvedAt = report.ResolvedAt,
            CreatedAt = report.CreatedAt
        };
    }
}
