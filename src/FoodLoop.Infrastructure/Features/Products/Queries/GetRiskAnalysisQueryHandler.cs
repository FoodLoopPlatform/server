using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Products.Queries;
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

namespace FoodLoop.Infrastructure.Features.Products.Queries;

public class GetRiskAnalysisQueryHandler : IRequestHandler<GetRiskAnalysisQuery, RiskAnalysisDto>
{
    private readonly ApplicationDbContext _db;

    public GetRiskAnalysisQueryHandler(ApplicationDbContext db) => _db = db;

    public async Task<RiskAnalysisDto> Handle(GetRiskAnalysisQuery request, CancellationToken cancellationToken)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.OwnerId == request.OwnerId && !o.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Organization", request.OwnerId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var products = await _db.Products
            .Where(p => p.OrganizationId == org.Id && !p.IsDeleted && p.Status == ProductStatus.Active)
            .ToListAsync(cancellationToken);

        var riskProducts = products.Select(p =>
        {
            int days = p.ExpirationDate.DayNumber - today.DayNumber;
            string level = days <= 1 ? "Critical" : days <= 3 ? "High" : days <= 7 ? "Medium" : "Low";
            return new RiskProductDto
            {
                Id = p.Id,
                Title = p.Title,
                OriginalPrice = p.OriginalPrice,
                DiscountedPrice = p.DiscountedPrice,
                QuantityAvailable = p.QuantityAvailable,
                ExpirationDate = p.ExpirationDate,
                DaysUntilExpiry = days,
                RiskLevel = level,
                PotentialLoss = p.DiscountedPrice * p.QuantityAvailable
            };
        }).ToList();

        return new RiskAnalysisDto
        {
            Summary = new RiskSummaryDto
            {
                TotalActiveProducts = riskProducts.Count,
                CriticalCount = riskProducts.Count(p => p.RiskLevel == "Critical"),
                HighCount = riskProducts.Count(p => p.RiskLevel == "High"),
                MediumCount = riskProducts.Count(p => p.RiskLevel == "Medium"),
                LowCount = riskProducts.Count(p => p.RiskLevel == "Low"),
                TotalAtRiskValue = riskProducts
                    .Where(p => p.RiskLevel != "Low")
                    .Sum(p => p.PotentialLoss)
            },
            Critical = riskProducts.Where(p => p.RiskLevel == "Critical").OrderBy(p => p.DaysUntilExpiry).ToList(),
            High     = riskProducts.Where(p => p.RiskLevel == "High").OrderBy(p => p.DaysUntilExpiry).ToList(),
            Medium   = riskProducts.Where(p => p.RiskLevel == "Medium").OrderBy(p => p.DaysUntilExpiry).ToList(),
            Low      = riskProducts.Where(p => p.RiskLevel == "Low").OrderBy(p => p.DaysUntilExpiry).ToList(),
        };
    }
}
