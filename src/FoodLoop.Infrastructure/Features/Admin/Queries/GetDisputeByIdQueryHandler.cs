using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin.Queries;

public class GetDisputeByIdQueryHandler : IRequestHandler<GetDisputeByIdQuery, DisputeDto>
{
    private readonly ApplicationDbContext _db;

    public GetDisputeByIdQueryHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DisputeDto> Handle(GetDisputeByIdQuery request, CancellationToken cancellationToken)
    {
        var report = await _db.ProductReports
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductReport), request.Id);

        var reporter = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == report.ReportedBy, cancellationToken);

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
