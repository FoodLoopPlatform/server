using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FoodLoop.Application.Common.DTOs.AI;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.AiIntegration.Queries;
using FoodLoop.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Features.AiIntegration.Queries;

public class GetPendingAiRecommendationsQueryHandler : IRequestHandler<GetPendingAiRecommendationsQuery, Result<IReadOnlyList<AiPricingRecommendationDto>>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetPendingAiRecommendationsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<AiPricingRecommendationDto>>> Handle(GetPendingAiRecommendationsQuery request, CancellationToken cancellationToken)
    {
        // Resolve the merchant's store
        var store = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.OwnerId == request.MerchantUserId && !o.IsDeleted, cancellationToken);

        if (store == null)
        {
            IReadOnlyList<AiPricingRecommendationDto> empty = Array.Empty<AiPricingRecommendationDto>();
            return Result<IReadOnlyList<AiPricingRecommendationDto>>.Ok(empty);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var recommendations = await _dbContext.AiPricingRecommendations
            .Include(r => r.Product!)
                .ThenInclude(p => p.Images)
            .Include(r => r.RiskAssessment)
            .Where(r => r.OrganizationId == store.Id && r.Status == AiRecommendationStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var dtos = recommendations.Select(r =>
        {
            var product = r.Product;
            var originalPrice = r.SnapshotOriginalPrice ?? (product != null ? product.OriginalPrice : 0m);
            var currentPrice = product != null ? product.DiscountedPrice : originalPrice;
            var discountPercentage = r.DiscountPercentage;
            var discountAmount = Math.Round(originalPrice * (discountPercentage / 100m), 2);
            var recommendedPrice = Math.Max(0m, originalPrice - discountAmount);
            var quantity = r.SnapshotQuantityAvailable ?? (product != null ? product.QuantityAvailable : 0);
            var expiry = product != null ? product.ExpirationDate : today;
            var daysRemaining = Math.Max(0, expiry.DayNumber - today.DayNumber);

            var imageUrl = product?.Images?.OrderBy(i => i.DisplayOrder)
                .Select(i => i.ImageUrl)
                .FirstOrDefault();

            var riskLevel = r.RiskAssessment != null
                ? r.RiskAssessment.RiskLevel.ToString()
                : (daysRemaining <= 1 ? "Critical" : daysRemaining <= 3 ? "High" : "Medium");

            return new AiPricingRecommendationDto(
                r.Id,
                r.ProductId,
                product != null ? product.Title : "Unknown Product",
                originalPrice,
                currentPrice,
                recommendedPrice,
                discountPercentage,
                discountAmount,
                quantity,
                expiry,
                daysRemaining,
                imageUrl,
                riskLevel,
                r.Reason,
                r.Confidence,
                r.ActionRequirement.ToString(),
                r.ActionReason,
                r.Status.ToString(),
                r.CorrelationId,
                r.CreatedAt
            );
        }).ToList();

        IReadOnlyList<AiPricingRecommendationDto> resultList = dtos;
        return Result<IReadOnlyList<AiPricingRecommendationDto>>.Ok(resultList);
    }
}
