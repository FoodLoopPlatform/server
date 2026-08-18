using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin.Queries;

public class GetDisputesQueryHandler : IRequestHandler<GetDisputesQuery, IReadOnlyList<DisputeDto>>
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public GetDisputesQueryHandler(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<DisputeDto>> Handle(GetDisputesQuery request, CancellationToken cancellationToken)
    {
        var query = _db.ProductReports
            .Include(r => r.Product)
            .AsQueryable();

        if (request.IsResolved.HasValue)
            query = query.Where(r => r.IsResolved == request.IsResolved.Value);

        var reports = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // Batch-load reporter names
        var reporterIds = reports.Select(r => r.ReportedBy).Distinct().ToList();
        var reporters = await _db.Users
            .Where(u => reporterIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return reports.Select(r => new DisputeDto
        {
            Id = r.Id,
            ProductId = r.ProductId,
            ProductTitle = r.Product?.Title ?? "Unknown",
            ReportedBy = r.ReportedBy,
            ReporterName = reporters.TryGetValue(r.ReportedBy, out var name) ? name : "User",
            Reason = r.Reason,
            Details = r.Details,
            IsResolved = r.IsResolved,
            AdminNote = r.AdminNote,
            ResolvedAt = r.ResolvedAt,
            CreatedAt = r.CreatedAt,
            ImageUrl = r.ImageUrl
        }).ToList();
    }
}
