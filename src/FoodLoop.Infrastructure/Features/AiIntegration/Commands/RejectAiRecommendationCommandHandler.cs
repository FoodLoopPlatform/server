using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.AiIntegration.Commands;
using FoodLoop.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FoodLoop.Infrastructure.Features.AiIntegration.Commands;

public class RejectAiRecommendationCommandHandler : IRequestHandler<RejectAiRecommendationCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<RejectAiRecommendationCommandHandler> _logger;

    public RejectAiRecommendationCommandHandler(
        IApplicationDbContext dbContext,
        ILogger<RejectAiRecommendationCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<Unit>> Handle(RejectAiRecommendationCommand request, CancellationToken cancellationToken)
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
            var actionReason = request.Reason ?? "Rejected by merchant.";

            // Claim/Lock Phase: transition Status from Pending -> Rejected using atomic ExecuteUpdateAsync
            int updatedRows = await _dbContext.AiPricingRecommendations
                .Where(r => r.Id == request.Id && r.OrganizationId == store.Id && r.Status == AiRecommendationStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, AiRecommendationStatus.Rejected)
                                          .SetProperty(r => r.ActionReason, actionReason),
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
                    // Belongs to another merchant's store! Throw UnauthorizedAccessException
                    throw new UnauthorizedAccessException("Merchant is not authorized to act on another store's recommendation.");
                }

                return Result<Unit>.Fail("Recommendation is not in Pending status.");
            }

            // Retrieve recommendation to log context info
            var rec = await _dbContext.AiPricingRecommendations
                .FirstAsync(r => r.Id == request.Id, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Assisted Pricing Recommendation rejected. Recommendation: {RecId}. CorrelationId: {CorrelationId}, Actor: {UserId}", request.Id, rec.CorrelationId, request.MerchantUserId);
            return Result<Unit>.Ok(Unit.Value);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Exception thrown during recommendation rejection. Recommendation: {RecId}.", request.Id);
            throw;
        }
    }
}
