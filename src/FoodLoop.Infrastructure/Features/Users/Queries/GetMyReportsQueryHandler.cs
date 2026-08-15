using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Users.Queries;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Users.Queries;

public class GetMyReportsQueryHandler : IRequestHandler<GetMyReportsQuery, IReadOnlyList<DisputeDto>>
{
    private readonly ApplicationDbContext _db;

    public GetMyReportsQueryHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DisputeDto>> Handle(GetMyReportsQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        var userName = user?.FullName ?? "User";

        var query = _db.ProductReports
            .Include(r => r.Product)
            .Where(r => r.ReportedBy == request.UserId);

        if (request.IsResolved.HasValue)
            query = query.Where(r => r.IsResolved == request.IsResolved.Value);

        var reports = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return reports.Select(r => new DisputeDto
        {
            Id = r.Id,
            ProductId = r.ProductId,
            ProductTitle = r.Product?.Title ?? "Unknown",
            ReportedBy = r.ReportedBy,
            ReporterName = userName,
            Reason = r.Reason,
            Details = r.Details,
            IsResolved = r.IsResolved,
            AdminNote = r.AdminNote,
            ResolvedAt = r.ResolvedAt,
            CreatedAt = r.CreatedAt
        }).ToList();
    }
}
