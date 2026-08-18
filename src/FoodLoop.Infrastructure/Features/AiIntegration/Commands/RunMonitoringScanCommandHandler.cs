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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.AiIntegration.Commands;

public class RunMonitoringScanCommandHandler : IRequestHandler<RunMonitoringScanCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAiServiceClient _aiClient;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly MonitoringScannerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RunMonitoringScanCommandHandler> _logger;

    public RunMonitoringScanCommandHandler(
        IApplicationDbContext dbContext,
        IAiServiceClient aiClient,
        ICorrelationIdAccessor correlationIdAccessor,
        IOptions<MonitoringScannerOptions> options,
        TimeProvider timeProvider,
        ILogger<RunMonitoringScanCommandHandler> logger)
    {
        _dbContext = dbContext;
        _aiClient = aiClient;
        _correlationIdAccessor = correlationIdAccessor;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<Unit>> Handle(RunMonitoringScanCommand request, CancellationToken cancellationToken)
    {
        // 1. Fetch active products
        var products = await _dbContext.Products
            .Include(p => p.Category)
            .Include(p => p.Organization)
            .Where(p => p.Status == ProductStatus.Active)
            .ToListAsync(cancellationToken);

        var settings = await _dbContext.SystemSettings.FirstOrDefaultAsync(cancellationToken);
        var floorPolicy = settings?.DefaultPriceFloorPolicy ?? FoodLoop.Domain.Enums.PriceFloorPolicy.DynamicAi;

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);
        var expirationCutoff = today.AddDays(_options.ExpirationThresholdDays);

        foreach (var product in products)
        {
            // 2. Resolve operating mode
            var org = product.Organization;
            if (org == null)
            {
                _logger.LogWarning("Product {ProductId} is not linked to an organization. Skipping.", product.Id);
                continue;
            }

            // Guard: Process ONLY Assisted or Autonomous. Skip Manual.
            if (org.AiOperatingMode == AiOperatingMode.Manual)
            {
                _logger.LogDebug("Organization {OrgId} is in Manual mode. Skipping product {ProductId}.", org.Id, product.Id);
                continue;
            }

            // Ensure correlation ID is set for this candidate's execution context
            var correlationId = _correlationIdAccessor.GetCorrelationId();
            using var scope = _logger.BeginScope(new System.Collections.Generic.Dictionary<string, object>
            {
                ["ProductId"] = product.Id,
                ["OrganizationId"] = org.Id,
                ["CorrelationId"] = correlationId
            });

            try
            {
                // 3. Candidate Selection checks
                // A. Expiration check
                var isNearingExpiry = product.ExpirationDate <= expirationCutoff;

                // B. Sales velocity check
                var cutoffDate = _timeProvider.GetUtcNow().AddDays(-30);
                var paidOrders = await _dbContext.Orders
                    .Where(o => o.PaymentStatus == PaymentStatus.Paid && o.CreatedAt >= cutoffDate)
                    .Select(o => new { o.Id, o.CreatedAt })
                    .ToListAsync(cancellationToken);

                var productItems = await _dbContext.OrderItems
                    .Where(oi => oi.ProductId == product.Id)
                    .Select(oi => new { oi.OrderId, oi.Quantity })
                    .ToListAsync(cancellationToken);

                var orderItems = (from oi in productItems
                                  join o in paidOrders on oi.OrderId equals o.Id
                                  select new { oi.Quantity, o.CreatedAt }).ToList();

                var metrics = SalesMetricsCalculator.Calculate(
                    orderItems.Select(oi => new SalesMetricsCalculator.OrderItemSummary { Quantity = oi.Quantity, CreatedAt = oi.CreatedAt }),
                    product.CreatedAt,
                    _timeProvider.GetUtcNow()
                );
                var salesVelocity = metrics.SalesVelocity;
                var historicalAvg = metrics.HistoricalAverageDailySales;

                var isLowVelocity = false;
                if (historicalAvg > 0)
                {
                    isLowVelocity = salesVelocity < (historicalAvg * _options.VelocityThresholdMultiplier);
                }

                // Must meet at least one candidate criterion to invoke AI analysis
                if (!isNearingExpiry && !isLowVelocity)
                {
                    _logger.LogDebug("Product {ProductId} does not meet expiration or velocity criteria. Skipping.", product.Id);
                    continue;
                }

                // 4. Construct MonitoringRequestDto
                var expiresAt = new DateTimeOffset(product.ExpirationDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                var hoursRemaining = (expiresAt - _timeProvider.GetUtcNow()).TotalHours;
                if (hoursRemaining < 0) hoursRemaining = 0;

                decimal priceFloor = PriceFloorCalculator.Calculate(product.OriginalPrice, floorPolicy);

                var requestDto = new MonitoringRequestDto(
                    Product: new MonitoringProductDto(
                        Id: product.Id.ToString(),
                        Name: product.Title,
                        Category: product.Category?.Name ?? "General"
                    ),
                    Inventory: new MonitoringInventoryDto(
                        Quantity: product.QuantityAvailable,
                        OriginalPrice: product.OriginalPrice,
                        CurrentPrice: product.DiscountedPrice,
                        PriceFloor: priceFloor
                    ),
                    Demand: new MonitoringDemandDto(
                        SalesVelocity: salesVelocity,
                        HistoricalSales: new MonitoringHistoricalSalesDto(historicalAvg)
                    ),
                    Expiry: new MonitoringExpiryDto(
                        ExpiresAt: expiresAt,
                        HoursRemaining: hoursRemaining
                    ),
                    Location: new MonitoringLocationDto(
                        Latitude: org.Latitude ?? 0.0,
                        Longitude: org.Longitude ?? 0.0,
                        StoreId: org.Id.ToString()
                    ),
                    StorePolicy: new MonitoringStorePolicyDto(
                        StoreId: org.Id.ToString(),
                        OperatingMode: org.AiOperatingMode.ToApiOperatingMode()
                    ),
                    Timestamp: _timeProvider.GetUtcNow()
                );

                // 5. Call AI Monitoring endpoint
                _logger.LogInformation("Calling AI monitoring analysis for Product {ProductId}.", product.Id);
                var response = await _aiClient.AnalyzeMonitoringAsync(requestDto, cancellationToken);

                // Parse Route string to AiRoute enum
                if (!Enum.TryParse<AiRoute>(response.Route, true, out var routeEnum))
                {
                    routeEnum = AiRoute.NO_ACTION;
                }

                // Map risk level
                if (!Enum.TryParse<AiRiskLevel>(response.RiskLevel, true, out var riskLevelEnum))
                {
                    riskLevelEnum = AiRiskLevel.LOW;
                }

                // 6. Persist AiRiskAssessment
                var isPricingStaged = routeEnum == AiRoute.PRICING;

                var riskAssessment = new AiRiskAssessment(
                    productId: product.Id,
                    riskLevel: riskLevelEnum,
                    route: routeEnum,
                    reason: response.Reason,
                    confidence: response.Confidence,
                    correlationId: correlationId,
                    isPricingStaged: isPricingStaged,
                    requestedContext: null
                )
                {
                    SnapshotOriginalPrice = product.OriginalPrice,
                    SnapshotQuantityAvailable = product.QuantityAvailable,
                    SnapshotProductStatus = product.Status
                };

                _dbContext.AiRiskAssessments.Add(riskAssessment);
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Saved AI Risk Assessment for Product {ProductId} with Route {Route} (Pricing Staged: {IsPricingStaged}).",
                    product.Id, routeEnum, isPricingStaged);
            }
            catch (Exception ex)
            {
                // Background Resilience: log error and continue scanning other products
                _logger.LogError(ex, "Failed to process monitoring scan for Product {ProductId} (Correlation ID: {CorrelationId}).",
                    product.Id, correlationId);
            }
        }

        return Result<Unit>.Ok(Unit.Value);
    }
}
