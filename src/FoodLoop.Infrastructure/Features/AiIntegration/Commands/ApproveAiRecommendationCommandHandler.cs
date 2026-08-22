using FoodLoop.Application.Common.Exceptions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.AiIntegration.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.AiIntegration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FoodLoop.Infrastructure.Features.AiIntegration.Commands;

public class ApproveAiRecommendationCommandHandler : IRequestHandler<ApproveAiRecommendationCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ApproveAiRecommendationCommandHandler> _logger;

    public ApproveAiRecommendationCommandHandler(
        IApplicationDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<ApproveAiRecommendationCommandHandler> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<Unit>> Handle(ApproveAiRecommendationCommand request, CancellationToken cancellationToken)
    {
        // Resolve the merchant's store
        var store = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.OwnerId == request.MerchantUserId && !o.IsDeleted, cancellationToken);

        if (store == null)
        {
            return Result<Unit>.Fail("Merchant store not found.");
        }

        var dbContext = (DbContext)_dbContext;
        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Claim/Lock Phase: transition Status from Pending -> Approved using atomic ExecuteUpdateAsync
            int updatedRows = await _dbContext.AiPricingRecommendations
                .Where(r => r.Id == request.Id && r.OrganizationId == store.Id && r.Status == AiRecommendationStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, AiRecommendationStatus.Approved)
                                          .SetProperty(r => r.ApprovedBy, request.MerchantUserId)
                                          .SetProperty(r => r.ApprovedAt, _timeProvider.GetUtcNow()),
                                    cancellationToken);

            if (updatedRows == 0)
            {
                // Determine why the lock/claim failed to return a proper error
                var exists = await _dbContext.AiPricingRecommendations
                    .AnyAsync(r => r.Id == request.Id, cancellationToken);

                if (!exists)
                {
                    return Result<Unit>.Fail("Recommendation not found.");
                }

                var belongsToStore = await _dbContext.AiPricingRecommendations
                    .AnyAsync(r => r.Id == request.Id && r.OrganizationId == store.Id, cancellationToken);

                if (!belongsToStore)
                {
                    // Belongs to another merchant's store! Throw UnauthorizedAccessException as required
                    throw new UnauthorizedAccessException("Merchant is not authorized to act on another store's recommendation.");
                }

                throw new ConflictException("Recommendation is not in Pending status.");
            }

            // Verify Phase: load recommendation and product details
            var rec = await _dbContext.AiPricingRecommendations
                .FirstAsync(r => r.Id == request.Id, cancellationToken);

            using var logScope = _logger.BeginScope(new System.Collections.Generic.Dictionary<string, object>
            {
                ["RecommendationId"] = rec.Id,
                ["ProductId"] = rec.ProductId,
                ["CorrelationId"] = rec.CorrelationId ?? string.Empty
            });

            var product = await _dbContext.Products
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == rec.ProductId, cancellationToken);

            if (product == null ||
                rec.SnapshotOriginalPrice == null ||
                rec.SnapshotQuantityAvailable == null ||
                rec.SnapshotProductStatus == null ||
                product.OriginalPrice != rec.SnapshotOriginalPrice.Value ||
                product.QuantityAvailable != rec.SnapshotQuantityAvailable.Value ||
                product.Status != rec.SnapshotProductStatus.Value ||
                product.Status != ProductStatus.Active)
            {
                await _dbContext.AiPricingRecommendations
                    .Where(r => r.Id == request.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, AiRecommendationStatus.Rejected)
                                              .SetProperty(r => r.ActionReason, "Stale Recommendation - Product State Changed"),
                                        cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                _logger.LogWarning("Freshness check failed on Approval for Product {ProductId}. Status set to Rejected.", rec.ProductId);
                return Result<Unit>.Fail("Approval failed: Stale Recommendation - Product State Changed.");
            }

            // Calculate proposed price from discount percentage
            var proposedPrice = Math.Round(product.OriginalPrice * (1.0m - rec.DiscountPercentage / 100.0m), 2);
            if (proposedPrice <= 0m)
            {
                proposedPrice = 0.01m; // Safety floor preventing negative/zero price
            }

            // SUCCESS PATH: Mutate price, write PriceHistory row, and leave Status = Approved with ExecutedAt set
            var history = new PriceHistory
            {
                ProductId = product.Id,
                OldOriginalPrice = product.OriginalPrice,
                OldDiscountedPrice = product.DiscountedPrice,
                NewOriginalPrice = product.OriginalPrice,
                NewDiscountedPrice = proposedPrice,
                ChangeReason = $"AI Assisted Approval by Store Owner (Correlation: {rec.CorrelationId})",
                ChangedBy = Guid.Empty
            };
            _dbContext.PriceHistories.Add(history);

            product.DiscountedPrice = proposedPrice;
            
            // Set ExecutedAt timestamp on recommendation
            rec.ExecutedAt = _timeProvider.GetUtcNow();
            rec.ActionReason = "Approved by merchant";

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Assisted Pricing Recommendation approved and applied by store owner. Product: {ProductId}, New Price: {Price}. CorrelationId: {CorrelationId}, Actor: {UserId}", product.Id, proposedPrice, rec.CorrelationId, request.MerchantUserId);
            return Result<Unit>.Ok(Unit.Value);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Exception thrown during recommendation approval. Recommendation: {RecId}.", request.Id);
            throw;
        }
    }
}
