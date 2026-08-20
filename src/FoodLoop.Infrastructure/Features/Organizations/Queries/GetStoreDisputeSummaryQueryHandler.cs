using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.Features.Organizations.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Organizations;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Organizations.Queries;

public class GetStoreDisputeSummaryQueryHandler : IRequestHandler<GetStoreDisputeSummaryQuery, StoreDisputeSummaryDto>
{
    private readonly ApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;

    public GetStoreDisputeSummaryQueryHandler(ApplicationDbContext db, IUnitOfWork unitOfWork)
    {
        _db = db;
        _unitOfWork = unitOfWork;
    }

    public async Task<StoreDisputeSummaryDto> Handle(GetStoreDisputeSummaryQuery request, CancellationToken cancellationToken)
    {
        var organization = await _unitOfWork.FindByOwnerOrThrowAsync(request.MerchantUserId, "Organization not found.", cancellationToken);

        var reports = await _db.ProductReports
            .Include(r => r.Product)
            .Where(r => r.Product != null && r.Product.OrganizationId == organization.Id)
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var totalResolved = reports.Count(r => r.IsResolved);
        var totalUnresolved = reports.Count(r => !r.IsResolved);

        var activeStrikes = reports
            .Where(r => !r.IsResolved &&
                        (r.Product!.ExpirationDate < today || r.Reason == "Expired" || r.Reason == "WrongExpiry"))
            .Select(r => r.ReportedBy)
            .Distinct()
            .Count();

        var settings = await _db.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == SystemSettings.SingletonId, cancellationToken);
        var maxAllowed = settings?.MaxExpiredReportsBeforeDeactivation ?? 5;

        string healthStatus;
        if (organization.VerificationStatus == VerificationStatus.Rejected || activeStrikes >= maxAllowed)
        {
            healthStatus = "Suspended";
        }
        else if (activeStrikes >= maxAllowed - 1)
        {
            healthStatus = "Critical";
        }
        else if (activeStrikes > 0)
        {
            healthStatus = "Warning";
        }
        else
        {
            healthStatus = "Good";
        }

        var repeatProducts = reports
            .Where(r => r.Product != null)
            .GroupBy(r => new { r.ProductId, r.Product!.Title })
            .Select(g => new RepeatProductDisputeDto
            {
                ProductId = g.Key.ProductId,
                ProductTitle = g.Key.Title,
                ReportCount = g.Count()
            })
            .OrderByDescending(p => p.ReportCount)
            .Take(10)
            .ToList();

        return new StoreDisputeSummaryDto
        {
            ActiveStrikes = activeStrikes,
            MaxAllowedStrikes = maxAllowed,
            HealthStatus = healthStatus,
            TotalResolvedDisputes = totalResolved,
            TotalUnresolvedDisputes = totalUnresolved,
            RepeatProducts = repeatProducts
        };
    }
}
