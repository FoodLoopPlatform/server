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
    private readonly IRealTimeNotificationService _notificationService;

    public ReportProductCommandHandler(
        ApplicationDbContext db,
        FoodLoop.Application.Common.Interfaces.IAuditLogService auditLogService)
        : this(db, auditLogService, null!)
    {
    }

    public ReportProductCommandHandler(
        ApplicationDbContext db,
        FoodLoop.Application.Common.Interfaces.IAuditLogService auditLogService,
        IRealTimeNotificationService notificationService)
    {
        _db = db;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
    }

    public async Task<Unit> Handle(ReportProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        var validReasons = new[] { "MisleadingInfo", "WrongExpiry", "Expired", "Spam", "Inappropriate", "Other" };
        if (!System.Array.Exists(validReasons, r => r == request.Reason))
            throw new ArgumentException($"Invalid reason. Allowed: {string.Join(", ", validReasons)}");

        if (request.ImageUrl != null && request.ImageUrl.Length > 500)
            throw new ArgumentException("ImageUrl must be 500 characters or fewer.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Count existing expired reports
        var dbExpiredCount = await _db.ProductReports
            .Include(r => r.Product)
            .Where(r => r.Product != null && r.Product.OrganizationId == product.OrganizationId &&
                        (r.Product.ExpirationDate < today || r.Reason == "Expired" || r.Reason == "WrongExpiry"))
            .CountAsync(cancellationToken);

        var isCurrentReportExpired = product.ExpirationDate < today || request.Reason == "Expired" || request.Reason == "WrongExpiry";
        var totalExpiredReports = dbExpiredCount + (isCurrentReportExpired ? 1 : 0);

        var report = new ProductReport
        {
            ProductId = request.ProductId,
            ReportedBy = request.ReportedBy,
            Reason = request.Reason,
            Details = request.Details,
            ImageUrl = request.ImageUrl
        };

        _db.ProductReports.Add(report);

        var settings = await _db.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == SystemSettings.SingletonId, cancellationToken);
        var threshold = settings?.MaxExpiredReportsBeforeDeactivation ?? 3;

        if (totalExpiredReports >= threshold)
        {
            var organization = await _db.Organizations
                .FirstOrDefaultAsync(o => o.Id == product.OrganizationId, cancellationToken)
                ?? throw new NotFoundException("Organization", product.OrganizationId);

            organization.VerificationStatus = VerificationStatus.Rejected;
            var notice = $"\n[{DateTimeOffset.UtcNow:u}] Auto-deactivated: Exceeded maximum allowed expired product reports ({totalExpiredReports}/{threshold}).";
            organization.AdminNote = (organization.AdminNote ?? "") + notice;
            organization.UpdatedAt = DateTimeOffset.UtcNow;

            var owner = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == organization.OwnerId, cancellationToken);
            if (owner != null)
            {
                owner.Status = UserStatus.Suspended;
            }
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
            await _notificationService.SendNotificationToRoleAsync(
                "Admin",
                "Product Reported",
                $"Product '{product.Title}' was reported. Reason: {request.Reason}.",
                "ProductReported",
                "ProductReport",
                report.Id,
                cancellationToken);
        }

        return Unit.Value;
    }
}
