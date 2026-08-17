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

        // Query all candidate products using IgnoreQueryFilters to include soft-deleted products
        var products = await _dbContext.Products
            .IgnoreQueryFilters()
            .Include(p => p.Category)
            .Where(p => p.QuantityAvailable == 0 || p.ExpirationDate < today || p.IsDeleted)
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            _logger.LogInformation("No candidate products found for historical ingestion sweep.");
            return Result<Unit>.Ok(Unit.Value);
        }

        _logger.LogInformation("Found {Count} candidate products for historical pricing check.", products.Count);

        var settings = await _dbContext.SystemSettings.FirstOrDefaultAsync(cancellationToken);
        var floorPolicy = settings?.DefaultPriceFloorPolicy ?? PriceFloorPolicy.DynamicAi;

        // Chunk products into batches based on configured BatchSize
        var batches = products.Chunk(_options.BatchSize);

        foreach (var batch in batches)
        {
            var productIds = batch.Select(p => p.Id).ToList();

            // Load all existing ingested episodes for these product IDs to enforce idempotency
            var existingEpisodes = await _dbContext.ProductPricingEpisodes
                .Where(pe => productIds.Contains(pe.ProductId))
                .ToListAsync(cancellationToken);

            // Load all paid orders containing items for the current batch of products to calculate metrics
            var orderItems = await _dbContext.Orders
                .Where(o => o.PaymentStatus == PaymentStatus.Paid)
                .SelectMany(o => o.Items.Where(oi => productIds.Contains(oi.ProductId)), (o, oi) => new { oi.ProductId, oi.Quantity, o.CreatedAt })
                .ToListAsync(cancellationToken);

            // Load price histories for the batch of products
            var priceHistories = await _dbContext.PriceHistories
                .Where(ph => productIds.Contains(ph.ProductId))
                .ToListAsync(cancellationToken);

            var eventsList = new List<HistoricalPricingEventDto>();
            var episodesToInsert = new List<ProductPricingEpisode>();

            foreach (var product in batch)
            {
                // Find discountEvent - the earliest PriceHistory row where NewDiscountedPrice < OldDiscountedPrice
                var discountEvent = priceHistories
                    .Where(ph => ph.ProductId == product.Id && 
                                 ph.NewDiscountedPrice < ph.OldDiscountedPrice &&
                                 !existingEpisodes.Any(pe => pe.EventId == $"ep-{product.Id}-{ph.Id}"))
                    .OrderBy(ph => ph.CreatedAt)
                    .FirstOrDefault();

                string candidateEventId;
                DateTimeOffset recordedAt;
                decimal originalPrice;
                decimal currentPrice;

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
                var ingestionRequest = new HistoricalIngestionRequestDto(eventsList);

                await _aiClient.IngestHistoricalPricingAsync(ingestionRequest, cancellationToken);

                // Batch succeeded: set correlation id on episodes and save to DB
                foreach (var episode in episodesToInsert)
                {
                    episode.IngestionCorrelationId = ingestionCorrelationId;
                }

                _dbContext.ProductPricingEpisodes.AddRange(episodesToInsert);
                await _dbContext.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("Successfully ingested {Count} historical pricing episodes.", episodesToInsert.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ingest batch of historical pricing events. Episodes will remain eligible for retry.");
                // Continue loop so subsequent batches can succeed
            }
        }

        return Result<Unit>.Ok(Unit.Value);
    }
}
