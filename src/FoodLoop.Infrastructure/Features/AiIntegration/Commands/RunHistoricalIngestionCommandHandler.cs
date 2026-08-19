using FoodLoop.Application.Common.DTOs.AI;
using FoodLoop.Application.Common.Extensions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Interfaces.AI;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.AiIntegration.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Options;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.AiIntegration.Commands;

public class RunHistoricalIngestionCommandHandler : IRequestHandler<RunHistoricalIngestionCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAiServiceClient _aiClient;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly TimeProvider _timeProvider;
    private readonly HistoricalIngestionOptions _options;
    private readonly ILogger<RunHistoricalIngestionCommandHandler> _logger;

    public RunHistoricalIngestionCommandHandler(
        IApplicationDbContext dbContext,
        IAiServiceClient aiClient,
        ICorrelationIdAccessor correlationIdAccessor,
        TimeProvider timeProvider,
        IOptions<HistoricalIngestionOptions> options,
        ILogger<RunHistoricalIngestionCommandHandler> logger)
    {
        _dbContext = dbContext;
        _aiClient = aiClient;
        _correlationIdAccessor = correlationIdAccessor;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<Unit>> Handle(RunHistoricalIngestionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running background historical pricing event ingestion sweep.");

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);

        // 1. Query all products that have pending (IngestedAt == null) episodes
        var pendingEpisodesList = await _dbContext.ProductPricingEpisodes
            .Where(pe => pe.IngestedAt == null)
            .ToListAsync(cancellationToken);

        var pendingProductIds = pendingEpisodesList.Select(pe => pe.ProductId).Distinct().ToList();

        var pendingProducts = await _dbContext.Products
            .IgnoreQueryFilters()
            .Include(p => p.Category)
            .Where(p => pendingProductIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        // 2. Query all candidate products based on current product state
        var standardProducts = await _dbContext.Products
            .IgnoreQueryFilters()
            .Include(p => p.Category)
            .Where(p => p.QuantityAvailable == 0 || p.ExpirationDate < today || p.IsDeleted)
            .ToListAsync(cancellationToken);

        // Combine both sets (deduped by Product.Id)
        var products = pendingProducts
            .UnionBy(standardProducts, p => p.Id)
            .ToList();

        if (products.Count == 0)
        {
            _logger.LogInformation("No candidate products found for historical ingestion sweep.");
            return Result<Unit>.Ok(Unit.Value);
        }

        _logger.LogInformation("Found {Count} candidate products for historical pricing check ({PendingCount} pending correction).", products.Count, pendingProducts.Count);

        var settings = await _dbContext.SystemSettings.FirstOrDefaultAsync(cancellationToken);
        var floorPolicy = settings?.DefaultPriceFloorPolicy ?? PriceFloorPolicy.DynamicAi;

        // Chunk products into batches based on configured BatchSize
        var batches = products.Chunk(_options.BatchSize);

        foreach (var batch in batches)
        {
            var productIds = batch.Select(p => p.Id).ToList();

            // Load all existing episodes for these product IDs
            var allEpisodes = await _dbContext.ProductPricingEpisodes
                .Where(pe => productIds.Contains(pe.ProductId))
                .ToListAsync(cancellationToken);

            var existingEpisodes = allEpisodes.Where(pe => pe.IngestedAt != null).ToList();

            var paidOrders = await _dbContext.Orders
                .Where(o => o.PaymentStatus == PaymentStatus.Paid)
                .Select(o => new { o.Id, o.CreatedAt })
                .ToListAsync(cancellationToken);

            var batchItems = await _dbContext.OrderItems
                .Where(oi => productIds.Contains(oi.ProductId))
                .Select(oi => new { oi.ProductId, oi.OrderId, oi.Quantity })
                .ToListAsync(cancellationToken);

            var orderItems = (from oi in batchItems
                              join o in paidOrders on oi.OrderId equals o.Id
                              select new { oi.ProductId, oi.Quantity, o.CreatedAt }).ToList();

            // Load price histories for the batch of products
            var priceHistories = await _dbContext.PriceHistories
                .Where(ph => productIds.Contains(ph.ProductId))
                .ToListAsync(cancellationToken);

            var eventsList = new List<HistoricalPricingEventDto>();
            var episodesToInsert = new List<ProductPricingEpisode>();

            foreach (var product in batch)
            {
                var pendingCorrection = allEpisodes.FirstOrDefault(pe => pe.ProductId == product.Id && pe.IngestedAt == null);

                string candidateEventId;
                DateTimeOffset recordedAt;
                decimal originalPrice;
                decimal currentPrice;
                PriceHistory? discountEvent = null;

                if (pendingCorrection != null)
                {
                    candidateEventId = pendingCorrection.EventId;
                    
                    if (candidateEventId.EndsWith("-nodisc"))
                    {
                        recordedAt = product.CreatedAt;
                        originalPrice = product.OriginalPrice;
                        currentPrice = product.DiscountedPrice;
                    }
                    else
                    {
                        var parts = candidateEventId.Split('-');
                        if (parts.Length >= 3 && Guid.TryParse(parts.Last(), out var phId))
                        {
                            discountEvent = priceHistories.FirstOrDefault(ph => ph.Id == phId);
                        }

                        if (discountEvent != null)
                        {
                            recordedAt = discountEvent.CreatedAt;
                            originalPrice = discountEvent.OldOriginalPrice;
                            currentPrice = discountEvent.NewDiscountedPrice;
                        }
                        else
                        {
                            recordedAt = pendingCorrection.RecordedAt;
                            originalPrice = product.OriginalPrice;
                            currentPrice = product.DiscountedPrice;
                        }
                    }
                }
                else
                {
                    discountEvent = priceHistories
                        .Where(ph => ph.ProductId == product.Id && 
                                     ph.NewDiscountedPrice < ph.OldDiscountedPrice &&
                                     !existingEpisodes.Any(pe => pe.EventId == $"ep-{product.Id}-{ph.Id}"))
                        .OrderBy(ph => ph.CreatedAt)
                        .FirstOrDefault();

                    if (discountEvent != null)
                    {
                        candidateEventId = $"ep-{product.Id}-{discountEvent.Id}";
                        recordedAt = discountEvent.CreatedAt;
                        originalPrice = discountEvent.OldOriginalPrice;
                        currentPrice = discountEvent.NewDiscountedPrice;
                    }
                    else
                    {
                        candidateEventId = $"ep-{product.Id}-nodisc";
                        recordedAt = product.CreatedAt;
                        originalPrice = product.OriginalPrice;
                        currentPrice = product.DiscountedPrice;
                    }
                }

                // Check membership using unique lookup on ProductId + EventId
                var isAlreadyIngested = existingEpisodes.Any(pe => pe.ProductId == product.Id && pe.EventId == candidateEventId);
                if (isAlreadyIngested)
                {
                    continue; // Already ingested: skip
                }

                // Compute hours_remaining
                var expiresAt = new DateTimeOffset(product.ExpirationDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                var hoursRemaining = (expiresAt - recordedAt).TotalHours;
                if (hoursRemaining < 0) hoursRemaining = 0;

                // Compute totalAllTimeUnitsSold
                var productOrderItems = orderItems.Where(oi => oi.ProductId == product.Id).ToList();
                var totalUnitsSold = productOrderItems.Sum(oi => oi.Quantity);

                // Calculate starting quantity
                var startingQuantity = product.QuantityAvailable + totalUnitsSold;

                // Compute sales metrics relative to recorded_at
                var metrics = SalesMetricsCalculator.Calculate(
                    productOrderItems.Select(oi => new SalesMetricsCalculator.OrderItemSummary { Quantity = oi.Quantity, CreatedAt = oi.CreatedAt }),
                    product.CreatedAt,
                    recordedAt
                );

                // Calculate units_sold_after_discount (sum of OrderItem.Quantity since discountEvent)
                var unitsSoldAfterDiscount = 0;
                if (discountEvent != null)
                {
                    unitsSoldAfterDiscount = productOrderItems
                        .Where(oi => oi.CreatedAt >= discountEvent.CreatedAt)
                        .Sum(oi => oi.Quantity);
                }

                // Calculate sell_through_rate
                var sellThroughRate = startingQuantity > 0 ? (double)unitsSoldAfterDiscount / startingQuantity : 0.0;

                // Calculate price floor reusing existing PriceFloorCalculator
                var priceFloor = PriceFloorCalculator.Calculate(originalPrice, floorPolicy);

                // Compute discount_percentage from snapshot values
                var discountPercentage = originalPrice > 0 ? (double)((originalPrice - currentPrice) / originalPrice * 100) : 0.0;

                // Compute outcome using explicit, mutually exclusive branch logic
                string outcome;
                if (product.QuantityAvailable == 0)
                {
                    outcome = "SOLD_OUT";
                }
                else if (totalUnitsSold > 0)
                {
                    outcome = "PARTIALLY_SOLD";
                }
                else if (product.ExpirationDate < today)
                {
                    outcome = "EXPIRED";
                }
                else if (product.IsDeleted)
                {
                    outcome = "UNSOLD";
                }
                else
                {
                    outcome = "UNSOLD";
                }

                // Validate and clamp discount_percentage
                if (discountPercentage < -0.01 || discountPercentage > 15.01)
                {
                    _logger.LogWarning("Product {ProductId} has discount {Discount}% which is outside the historical schema bounds [-0.01, 15.01]. Skipping this individual episode.", product.Id, discountPercentage);
                    continue; // Skip this individual episode, do not ingest
                }
                discountPercentage = Math.Clamp(discountPercentage, 0.0, 15.0);

                // Override with corrected snapshot fields if a pending correction exists
                var pendingEpisode = allEpisodes.FirstOrDefault(pe => pe.ProductId == product.Id && pe.EventId == candidateEventId && pe.IngestedAt == null);
                if (pendingEpisode != null)
                {
                    discountPercentage = pendingEpisode.DiscountPercentage;
                    sellThroughRate = pendingEpisode.SellThroughRate;
                    outcome = pendingEpisode.Outcome;
                }

                eventsList.Add(new HistoricalPricingEventDto(
                    EventId: candidateEventId,
                    StoreId: product.OrganizationId.ToString(),
                    ProductId: product.Id.ToString(),
                    Category: product.Category?.Name ?? "General",
                    RecordedAt: recordedAt,
                    Quantity: startingQuantity,
                    CurrentPrice: currentPrice,
                    OriginalPrice: originalPrice,
                    PriceFloor: priceFloor,
                    SalesVelocity: metrics.SalesVelocity,
                    HistoricalAverageDailySales: metrics.HistoricalAverageDailySales,
                    HoursRemaining: hoursRemaining,
                    DiscountPercentage: discountPercentage,
                    UnitsSoldAfterDiscount: unitsSoldAfterDiscount,
                    SellThroughRate: sellThroughRate,
                    Outcome: outcome
                ));

                episodesToInsert.Add(new ProductPricingEpisode
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    EventId = candidateEventId,
                    RecordedAt = recordedAt,
                    IngestedAt = _timeProvider.GetUtcNow(),
                    Outcome = outcome,
                    DiscountPercentage = discountPercentage,
                    SellThroughRate = sellThroughRate
                });
            }

            if (eventsList.Count == 0)
            {
                continue;
            }

            // Ingest this batch to the Python AI service
            try
            {
                var ingestionCorrelationId = _correlationIdAccessor.GetCorrelationId();
                using var logScope = _logger.BeginScope(new System.Collections.Generic.Dictionary<string, object>
                {
                    ["CorrelationId"] = ingestionCorrelationId
                });

                var ingestionRequest = new HistoricalIngestionRequestDto(eventsList);

                var response = await _aiClient.IngestHistoricalPricingAsync(ingestionRequest, cancellationToken);

                // Filter episodes to insert: only save/update those that are in response.DocumentIds (successfully upserted/accepted)
                var successfulEpisodes = new List<ProductPricingEpisode>();
                foreach (var episode in episodesToInsert)
                {
                    var isExplicitSuccess = response.DocumentIds != null && 
                                            (response.DocumentIds.Contains(episode.EventId) || response.DocumentIds.Contains($"doc-{episode.EventId}"));
                    
                    var isLegacyMockPass = (response.DocumentIds == null || response.DocumentIds.Count == 0) && response.FailedCount == 0;

                    if (isExplicitSuccess || isLegacyMockPass)
                    {
                        var existingPending = allEpisodes.FirstOrDefault(pe => pe.ProductId == episode.ProductId && pe.EventId == episode.EventId && pe.IngestedAt == null);
                        if (existingPending != null)
                        {
                            existingPending.IngestedAt = _timeProvider.GetUtcNow();
                            existingPending.IngestionCorrelationId = ingestionCorrelationId;
                            // Synchronize corrected snapshot fields on persistence just in case
                            existingPending.DiscountPercentage = episode.DiscountPercentage;
                            existingPending.SellThroughRate = episode.SellThroughRate;
                            existingPending.Outcome = episode.Outcome;
                            
                            _dbContext.ProductPricingEpisodes.Update(existingPending);
                        }
                        else
                        {
                            episode.IngestionCorrelationId = ingestionCorrelationId;
                            _dbContext.ProductPricingEpisodes.Add(episode);
                        }
                        successfulEpisodes.Add(episode);
                    }
                    else
                    {
                        _logger.LogWarning("Episode {EventId} was not in the successful document IDs list from ingestion response. Skipping persistence so it remains eligible for retry.", episode.EventId);
                    }
                }

                if (successfulEpisodes.Count > 0)
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                
                _logger.LogInformation("Successfully ingested {Count} historical pricing episodes. (Response counts - Accepted: {Accepted}, Upserted: {Upserted}, Failed: {Failed})", 
                    successfulEpisodes.Count, response.AcceptedCount, response.UpsertedCount, response.FailedCount);
            }
            catch (Exception ex)
            {
                var ingestionCorrelationId = _correlationIdAccessor.GetCorrelationId();
                using var logScope = _logger.BeginScope(new System.Collections.Generic.Dictionary<string, object>
                {
                    ["CorrelationId"] = ingestionCorrelationId
                });
                _logger.LogError(ex, "Failed to ingest batch of historical pricing events. Episodes will remain eligible for retry.");
                // Continue loop so subsequent batches can succeed
            }
        }

        return Result<Unit>.Ok(Unit.Value);
    }
}
