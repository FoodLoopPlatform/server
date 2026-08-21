using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FoodLoop.Application.Common.DTOs.AI;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Infrastructure.Integrations.AiService;
using Microsoft.Extensions.Logging;
using Moq;
using Polly;
using Polly.Registry;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Integrations;

[Trait("Category", "LiveAiIntegration")]
public class AiServiceLiveIntegrationTests
{
    private const string LiveAiBaseUrl = "http://3.94.7.125:8000";

    private readonly AiServiceClient _client;

    public AiServiceLiveIntegrationTests()
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(LiveAiBaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        var pipelineRegistry = new ResiliencePipelineRegistry<string>();
        
        // Register passthrough business pipeline for live test
        pipelineRegistry.TryAdd("AiServiceBusinessPipeline", new ResiliencePipelineBuilder<HttpResponseMessage>().Build());
        pipelineRegistry.TryAdd("AiServiceHealthPipeline", new ResiliencePipelineBuilder<HttpResponseMessage>().Build());

        var mockLogger = new Mock<ILogger<AiServiceClient>>();
        var mockCorrelation = new Mock<ICorrelationIdAccessor>();
        mockCorrelation.Setup(c => c.GetCorrelationId()).Returns("live-test-corr-id-123");

        _client = new AiServiceClient(httpClient, pipelineRegistry, mockLogger.Object, mockCorrelation.Object);
    }

    [Fact]
    public async Task Live_GetHealth_ReturnsOkStatus()
    {
        var health = await _client.GetHealthAsync();
        health.Should().NotBeNull();
        health.Status.Should().Be("ok");
    }

    [Fact]
    public async Task Live_GetReady_ReturnsReadyStatus()
    {
        var ready = await _client.GetReadyAsync();
        ready.Should().NotBeNull();
        ready.Status.Should().Be("ready");
    }

    [Fact]
    public async Task Live_GetVersion_ReturnsValidAppVersion()
    {
        var version = await _client.GetVersionAsync();
        version.Should().NotBeNull();
        version.AppName.Should().Be("FoodLoop AI Service");
        version.Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task Live_AnalyzeMonitoring_ReturnsValidRouteAndRisk()
    {
        var request = new MonitoringRequestDto(
            Product: new MonitoringProductDto("prod-100", "Fresh Whole Milk 1L", "Dairy"),
            Inventory: new MonitoringInventoryDto(40, 45.0m, 45.0m, 25.0m),
            Demand: new MonitoringDemandDto(2.5, new MonitoringHistoricalSalesDto(4.0)),
            Expiry: new MonitoringExpiryDto(DateTimeOffset.UtcNow.AddHours(24), 24.0),
            Location: new MonitoringLocationDto(30.0444, 31.2357, "store-cairo-01"),
            StorePolicy: new MonitoringStorePolicyDto("store-cairo-01", "autonomous"),
            Timestamp: DateTimeOffset.UtcNow
        );

        var response = await _client.AnalyzeMonitoringAsync(request);
        
        response.Should().NotBeNull();
        response.Route.Should().NotBeNullOrWhiteSpace();
        response.RiskLevel.Should().NotBeNullOrWhiteSpace();
        response.Confidence.Should().BeInRange(0.0, 1.0);
        response.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Live_RecommendPricing_ReturnsValidDecisions()
    {
        var request = new PricingBatchRequestDto(
            StoreId: "store-cairo-01",
            StorePolicy: new PricingStorePolicyDto("store-cairo-01", "autonomous"),
            Products: new List<PricingProductRequestDto>
            {
                new(
                    ProductId: "prod-100",
                    ProductName: "Fresh Whole Milk 1L",
                    Category: "Dairy",
                    Inventory: new PricingInventoryDto(40, 45.0m, 45.0m, 25.0m),
                    Demand: new PricingDemandDto(2.5, new PricingHistoricalSalesDto(4.0)),
                    Expiry: new PricingExpiryDto(DateTimeOffset.UtcNow.AddHours(24), 24.0),
                    RiskAssessment: new PricingRiskAssessmentDto("CRITICAL", "High expiry pressure", 0.95)
                )
            }
        );

        var response = await _client.RecommendPricingAsync(request);

        response.Should().NotBeNull();
        response.StoreId.Should().Be("store-cairo-01");
        response.Decisions.Should().HaveCount(1);
        
        var decision = response.Decisions[0];
        decision.ProductId.Should().Be("prod-100");
        decision.DiscountPercentage.Should().BeInRange(0.0, 15.0);
        decision.Confidence.Should().BeInRange(0.0, 1.0);
        decision.Reason.Should().NotBeNullOrWhiteSpace();
        decision.ActionRequirement.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Live_IngestHistoricalPricing_ReturnsAcceptedCount()
    {
        var request = new HistoricalIngestionRequestDto(
            Events: new List<HistoricalPricingEventDto>
            {
                new(
                    EventId: Guid.NewGuid().ToString(),
                    StoreId: "store-cairo-01",
                    ProductId: "prod-100",
                    Category: "Dairy",
                    RecordedAt: DateTimeOffset.UtcNow,
                    Quantity: 50,
                    CurrentPrice: 45.0m,
                    OriginalPrice: 50.0m,
                    PriceFloor: 25.0m,
                    SalesVelocity: 5.0,
                    HistoricalAverageDailySales: 6.0,
                    HoursRemaining: 24.0,
                    DiscountPercentage: 15.0,
                    UnitsSoldAfterDiscount: 45,
                    SellThroughRate: 0.9,
                    Outcome: "SOLD_OUT"
                )
            }
        );

        var response = await _client.IngestHistoricalPricingAsync(request);

        response.Should().NotBeNull();
        response.AcceptedCount.Should().Be(1);
        response.UpsertedCount.Should().Be(1);
        response.FailedCount.Should().Be(0);
        response.DocumentIds.Should().NotBeEmpty();
    }
}
