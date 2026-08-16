using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Organizations.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Organizations.Commands;

public class ResolveStoreDisputeCommandHandler : IRequestHandler<ResolveStoreDisputeCommand, DisputeDto>
{
    private readonly ApplicationDbContext _db;
    private readonly FoodLoop.Application.Common.Interfaces.IAuditLogService _auditLogService;

    public ResolveStoreDisputeCommandHandler(ApplicationDbContext db, FoodLoop.Application.Common.Interfaces.IAuditLogService auditLogService)
    {
        _db = db;
        _auditLogService = auditLogService;
    }

    public async Task<DisputeDto> Handle(ResolveStoreDisputeCommand request, CancellationToken cancellationToken)
    {
        if (request.RefundAmount < 0)
        {
            throw new ArgumentException("Refund amount must be zero or positive.");
        }

        // 1. Fetch the dispute
        var report = await _db.ProductReports
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == request.DisputeId, cancellationToken)
            ?? throw new NotFoundException("Dispute", request.DisputeId);

        if (report.IsResolved)
        {
            throw new InvalidOperationException("This dispute has already been resolved.");
        }

        // 2. Verify that this dispute belongs to the merchant's store
        var store = await _db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OwnerId == request.MerchantUserId && !o.IsDeleted, cancellationToken)
            ?? throw new UnauthorizedAccessException("Merchant organization not found.");

        if (report.Product == null || report.Product.OrganizationId != store.Id)
        {
            throw new UnauthorizedAccessException("You are not authorized to resolve this dispute as this product does not belong to your store.");
        }

        // 3. Apply refund to the user's wallet if amount > 0
        if (request.RefundAmount > 0)
        {
            var user = await _db.Users.FindAsync(new object[] { report.ReportedBy }, cancellationToken)
                ?? throw new NotFoundException("User (Reporter)", report.ReportedBy);

            user.WalletBalance += request.RefundAmount;

            var transaction = new WalletTransaction
            {
                UserId = report.ReportedBy,
                Amount = request.RefundAmount,
                Type = "Refund",
                ReferenceId = report.Id.ToString(),
                Description = $"Merchant refund for resolved dispute of product '{report.Product.Title}'."
            };

            _db.WalletTransactions.Add(transaction);
        }

        // 4. Mark dispute as resolved
        report.IsResolved = true;
        report.AdminNote = $"Merchant Resolution: {request.MerchantNote}";
        if (request.RefundAmount > 0)
        {
            report.AdminNote += $" (Refunded {request.RefundAmount:F2} to wallet)";
        }
        report.ResolvedAt = DateTimeOffset.UtcNow;

        _db.ProductReports.Update(report);
        await _db.SaveChangesAsync(cancellationToken);

        // 5. Audit Log
        await _auditLogService.LogAsync(
            request.MerchantUserId,
            store.Id,
            "DisputeResolved",
            "Product Dispute Resolved by Store",
            $"Store owner resolved dispute for product '{report.Product.Title}'. Resolution: {request.MerchantNote}. Refund: {request.RefundAmount:F2}",
            null,
            cancellationToken);

        var reporter = await _db.Users.FindAsync(new object[] { report.ReportedBy }, cancellationToken);

        return new DisputeDto
        {
            Id = report.Id,
            ProductId = report.ProductId,
            ProductTitle = report.Product.Title,
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
