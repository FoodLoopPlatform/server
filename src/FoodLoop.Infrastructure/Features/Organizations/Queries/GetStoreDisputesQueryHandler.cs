using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Organizations.Queries;
using FoodLoop.Infrastructure.Features.Organizations;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Organizations.Queries;

public class GetStoreDisputesQueryHandler : IRequestHandler<GetStoreDisputesQuery, IReadOnlyList<DisputeDto>>
{
    private readonly ApplicationDbContext _db;
    private readonly FoodLoop.Application.Common.Interfaces.IUnitOfWork _unitOfWork;

    public GetStoreDisputesQueryHandler(ApplicationDbContext db, FoodLoop.Application.Common.Interfaces.IUnitOfWork unitOfWork)
    {
        _db = db;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<DisputeDto>> Handle(GetStoreDisputesQuery request, CancellationToken cancellationToken)
    {
        var organization = await _unitOfWork.FindByOwnerOrThrowAsync(request.OwnerId, "Organization not found.", cancellationToken);

        var query = _db.ProductReports
            .Include(r => r.Product)
            .Where(r => r.Product != null && r.Product.OrganizationId == organization.Id);

        if (request.IsResolved.HasValue)
            query = query.Where(r => r.IsResolved == request.IsResolved.Value);

        var reports = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

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
            CreatedAt = r.CreatedAt
        }).ToList();
    }
}
