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

        var recommendations = await _dbContext.AiPricingRecommendations
            .Include(r => r.Product)
            .Where(r => r.OrganizationId == store.Id && r.Status == AiRecommendationStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new AiPricingRecommendationDto(
                r.Id,
                r.ProductId,
                r.Product != null ? r.Product.Title : string.Empty,
                r.DiscountPercentage,
                r.Reason,
                r.Confidence,
                r.ActionRequirement.ToString(),
                r.ActionReason,
                r.Status.ToString(),
                r.CorrelationId,
                r.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        IReadOnlyList<AiPricingRecommendationDto> resultList = recommendations;
        return Result<IReadOnlyList<AiPricingRecommendationDto>>.Ok(resultList);
    }
}
