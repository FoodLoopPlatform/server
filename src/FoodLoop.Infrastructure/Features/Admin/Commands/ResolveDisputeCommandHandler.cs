using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin.Commands;

public class ResolveDisputeCommandHandler : IRequestHandler<ResolveDisputeCommand, DisputeDto>
{
    private readonly ApplicationDbContext _db;
    private readonly FoodLoop.Application.Common.Interfaces.IAuditLogService _auditLogService;

    public ResolveDisputeCommandHandler(ApplicationDbContext db, FoodLoop.Application.Common.Interfaces.IAuditLogService auditLogService)
    {
        _db = db;
        _auditLogService = auditLogService;
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
