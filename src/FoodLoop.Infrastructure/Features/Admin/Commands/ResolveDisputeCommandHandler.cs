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

    public ResolveDisputeCommandHandler(ApplicationDbContext db) => _db = db;

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
