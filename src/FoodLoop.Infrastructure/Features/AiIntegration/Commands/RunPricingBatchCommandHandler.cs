using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FoodLoop.Application.Common.DTOs.AI;
using FoodLoop.Application.Common.Exceptions.AI;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Interfaces.AI;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.AiIntegration.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FoodLoop.Infrastructure.Options;

namespace FoodLoop.Infrastructure.Features.AiIntegration.Commands;

public class RunPricingBatchCommandHandler : IRequestHandler<RunPricingBatchCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAiServiceClient _aiClient;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly TimeProvider _timeProvider;
    private readonly AiServiceOptions _aiOptions;
    private readonly ILogger<RunPricingBatchCommandHandler> _logger;

    public RunPricingBatchCommandHandler(
        IApplicationDbContext dbContext,
        IAiServiceClient aiClient,
        ICorrelationIdAccessor correlationIdAccessor,
        TimeProvider timeProvider,
        ILogger<RunPricingBatchCommandHandler> logger,
        IOptions<AiServiceOptions>? aiOptions = null)
    {
        _dbContext = dbContext;
        _aiClient = aiClient;
        _correlationIdAccessor = correlationIdAccessor;
        _timeProvider = timeProvider;
        _aiOptions = aiOptions?.Value ?? new AiServiceOptions();
        _logger = logger;
    }

    public async Task<Result<Unit>> Handle(RunPricingBatchCommand request, CancellationToken cancellationToken)
    {
        // 1. Query staged AiRiskAssessment rows where IsPricingStaged = true AND no AiPricingRecommendation exists
        var stagedCandidates = await _dbContext.AiRiskAssessments
            .Include(ara => ara.Product)
                .ThenInclude(p => p!.Category)
            .Include(ara => ara.Product)
                .ThenInclude(p => p!.Organization)
            .Where(ara => ara.IsPricingStaged &&
                          ara.Product != null &&
                          ara.Product.Organization != null &&
                          !_dbContext.AiPricingRecommendations.Any(apr => apr.RiskAssessmentId == ara.Id))
            .ToListAsync(cancellationToken);

        if (stagedCandidates.Count == 0)
        {
            _logger.LogInformation("No staged AI risk assessment candidates found for batch pricing execution.");
            return Result<Unit>.Ok(Unit.Value);
        }

        _logger.LogInformation("Found {Count} staged candidates for batch pricing.", stagedCandidates.Count);

        // 2. Fetch platform system settings to calculate price floors
        var settings = await _dbContext.SystemSettings.FirstOrDefaultAsync(cancellationToken);
        var floorPolicy = settings?.DefaultPriceFloorPolicy ?? PriceFloorPolicy.DynamicAi;

        // 3. Group candidates by store (OrganizationId)
        var storeGroups = stagedCandidates
            .GroupBy(ara => ara.Product!.OrganizationId);

        foreach (var storeGroup in storeGroups)
        {
            var organizationId = storeGroup.Key;
            var candidates = storeGroup.ToList();
            var org = candidates.First().Product!.Organization!;

            // Defensive guard: skip if store operating mode has changed to Manual in the meantime
            if (org.AiOperatingMode == AiOperatingMode.Manual)
            {
                _logger.LogWarning("Defensive Guard: Store {OrgId} is currently in Manual mode. Skipping batch pricing for its candidates.", org.Id);
                continue;
            }

            // Reuse Correlation ID from first candidate or access ambient trace
            var correlationId = candidates.First().CorrelationId;
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = _correlationIdAccessor.GetCorrelationId();
            }

            using var logScope = _logger.BeginScope(new Dictionary<string, object>
            {
                { "StoreId", org.Id },
                { "CorrelationId", correlationId }
            });

            // CHUNKING: Chunk candidates by MaxPricingBatchSize (default 50)
            int batchSize = _aiOptions.MaxPricingBatchSize;
            if (batchSize < 1 || batchSize > 1000)
            {
                batchSize = 50;
            }

            var candidateChunks = candidates
                .Select((c, index) => new { Index = index, Value = c })
                .GroupBy(x => x.Index / batchSize)
                .Select(g => g.Select(x => x.Value).ToList())
                .ToList();

            foreach (var chunk in candidateChunks)
            {
                // 4. Map candidates to PricingBatchRequestDto
                var productRequests = new List<PricingProductRequestDto>();
                foreach (var candidate in chunk)
                {
                    var product = candidate.Product!;
                    
                    // Fetch last 30 days of paid orders to compute product velocity/demand metrics
                    var cutoffDate = _timeProvider.GetUtcNow().AddDays(-30);
                    var orderItems = await _dbContext.Orders
                        .Where(o => o.PaymentStatus == PaymentStatus.Paid && o.CreatedAt >= cutoffDate)
                        .SelectMany(o => o.Items.Where(oi => oi.ProductId == product.Id), (o, oi) => new { oi.Quantity, o.CreatedAt })
                        .ToListAsync(cancellationToken);

                    var metrics = SalesMetricsCalculator.Calculate(
                        orderItems.Select(oi => new SalesMetricsCalculator.OrderItemSummary { Quantity = oi.Quantity, CreatedAt = oi.CreatedAt }),
                        product.CreatedAt,
                        _timeProvider.GetUtcNow()
                    );
                    var salesVelocity = metrics.SalesVelocity;
                    var historicalAvg = metrics.HistoricalAverageDailySales;

                    var priceFloor = PriceFloorCalculator.Calculate(product.OriginalPrice, floorPolicy);

                    var expiresAt = new DateTimeOffset(product.ExpirationDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                    var hoursRemaining = (expiresAt - _timeProvider.GetUtcNow()).TotalHours;
                    if (hoursRemaining < 0) hoursRemaining = 0;

                    productRequests.Add(new PricingProductRequestDto(
                        ProductId: product.Id.ToString(),
                        ProductName: product.Title,
                        Category: product.Category?.Name ?? "General",
                        Inventory: new PricingInventoryDto(
                            Quantity: product.QuantityAvailable,
                            OriginalPrice: product.OriginalPrice,
                            CurrentPrice: product.DiscountedPrice,
                            PriceFloor: priceFloor
                        ),
                        Demand: new PricingDemandDto(
                            SalesVelocity: salesVelocity,
                            HistoricalSales: new PricingHistoricalSalesDto(historicalAvg)
                        ),
                        Expiry: new PricingExpiryDto(
                            ExpiresAt: expiresAt,
                            HoursRemaining: hoursRemaining
                        ),
                        RiskAssessment: new PricingRiskAssessmentDto(
                            RiskLevel: candidate.RiskLevel.ToString(),
                            Reason: candidate.Reason,
                            Confidence: candidate.Confidence
                        )
                    ));
                }

                var batchRequest = new PricingBatchRequestDto(
                    StoreId: org.Id.ToString(),
                    StorePolicy: new PricingStorePolicyDto(
                        StoreId: org.Id.ToString(),
                        OperatingMode: org.AiOperatingMode.ToString().ToLowerInvariant()
                    ),
                    Products: productRequests
                );

                // 5. Invoke recommend pricing batch via client with resilience catch block
                try
                {
                    var response = await _aiClient.RecommendPricingAsync(batchRequest, cancellationToken);
                    
                    foreach (var decision in response.Decisions)
                    {
                        if (!Guid.TryParse(decision.ProductId, out var productId))
                        {
                            _logger.LogError("Invalid ProductId '{ProdId}' in AI response decision. Skipping.", decision.ProductId);
                            continue;
                        }

                        var candidate = chunk.FirstOrDefault(c => c.ProductId == productId);
                        if (candidate == null)
                        {
                            _logger.LogWarning("Candidate not found in current batch chunk for ProductId '{ProductId}'. Skipping.", productId);
                            continue;
                        }

                        // Map enums
                        if (!Enum.TryParse<AiActionRequirement>(decision.ActionRequirement, out var actionRequirement))
                        {
                            actionRequirement = AiActionRequirement.APPROVAL_REQUIRED;
                        }

                        var finalStatus = AiRecommendationStatus.Pending;
                        var finalReason = decision.Reason;
                        var finalActionReason = decision.ActionReason;

                        if (org.AiOperatingMode == AiOperatingMode.Autonomous)
                        {
                            var liveProduct = await _dbContext.Products
                                .IgnoreQueryFilters()
                                .FirstOrDefaultAsync(p => p.Id == candidate.ProductId, cancellationToken);

                            if (liveProduct == null ||
                                candidate.SnapshotOriginalPrice == null ||
                                candidate.SnapshotQuantityAvailable == null ||
                                candidate.SnapshotProductStatus == null ||
                                liveProduct.OriginalPrice != candidate.SnapshotOriginalPrice.Value ||
                                liveProduct.QuantityAvailable != candidate.SnapshotQuantityAvailable.Value ||
                                liveProduct.Status != candidate.SnapshotProductStatus.Value ||
                                liveProduct.Status != ProductStatus.Active)
                            {
                                finalStatus = AiRecommendationStatus.Rejected;
                                finalReason = "Stale Recommendation - Product State Changed";
                                finalActionReason = "Stale Recommendation - Product State Changed";
                                _logger.LogWarning("Autonomous execution rejected: Stale Recommendation - Product State Changed for Product {ProductId}.", candidate.ProductId);
                            }
                            else
                            {
                                // Independently re-validate the recommended discount against the current price floor
                                var computedFloor = PriceFloorCalculator.Calculate(liveProduct.OriginalPrice, floorPolicy);
                                var proposedPrice = liveProduct.OriginalPrice * (1.0m - (decimal)decision.DiscountPercentage / 100.0m);

                                if (proposedPrice >= computedFloor)
                                {
                                    var history = new PriceHistory
                                    {
                                        ProductId = liveProduct.Id,
                                        OldOriginalPrice = liveProduct.OriginalPrice,
                                        OldDiscountedPrice = liveProduct.DiscountedPrice,
                                        NewOriginalPrice = liveProduct.OriginalPrice,
                                        NewDiscountedPrice = proposedPrice,
                                        ChangeReason = $"AI Autonomous Pricing (Correlation: {correlationId})",
                                        ChangedBy = Guid.Empty
                                    };
                                    _dbContext.PriceHistories.Add(history);

                                    liveProduct.DiscountedPrice = proposedPrice;
                                    finalStatus = AiRecommendationStatus.AutoExecuted;
                                }
                                else
                                {
                                    finalStatus = AiRecommendationStatus.Rejected;
                                    finalReason = $"[Rejected - Price Floor Violation] Proposed price {proposedPrice:C} falls below calculated price floor {computedFloor:C}. AI recommended: {decision.DiscountPercentage}%. Reason: {decision.Reason}";
                                    finalActionReason = $"[Rejected - Price Floor Violation] Proposed price {proposedPrice:C} falls below calculated price floor {computedFloor:C}. AI recommended: {decision.DiscountPercentage}%. Reason: {decision.Reason}";
                                    _logger.LogWarning("Price floor violation for Product {ProductId}: proposed price {Proposed} is below calculated floor {Floor}.", liveProduct.Id, proposedPrice, computedFloor);
                                }
                            }
                        }
                        else
                        {
                            // Assisted mode persists as Pending without price mutation
                            finalStatus = AiRecommendationStatus.Pending;
                        }

                        var recommendation = new AiPricingRecommendation(
                            productId: candidate.ProductId,
                            organizationId: org.Id,
                            discountPercentage: (decimal)decision.DiscountPercentage,
                            reason: finalReason,
                            confidence: decision.Confidence,
                            actionRequirement: actionRequirement,
                            actionReason: finalActionReason,
                            correlationId: correlationId,
                            status: finalStatus,
                            riskAssessmentId: candidate.Id
                        )
                        {
                            SnapshotOriginalPrice = candidate.SnapshotOriginalPrice,
                            SnapshotQuantityAvailable = candidate.SnapshotQuantityAvailable,
                            SnapshotProductStatus = candidate.SnapshotProductStatus
                        };

                        _dbContext.AiPricingRecommendations.Add(recommendation);
                    }

                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (AiServiceContractException contractEx)
                {
                    _logger.LogError(contractEx, "Contract validation failed for Store {StoreId} pricing batch chunk. Skipping this chunk.", org.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception while executing pricing batch chunk for Store {StoreId}. Candidate updates aborted for this chunk.", org.Id);
                }
            }
        }

        return Result<Unit>.Ok(Unit.Value);
    }
}
