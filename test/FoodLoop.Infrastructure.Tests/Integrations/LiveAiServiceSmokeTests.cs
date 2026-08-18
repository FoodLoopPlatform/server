using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FoodLoop.Application.Common.DTOs.AI;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Interfaces.AI;
using FoodLoop.Infrastructure.Integrations.AiService;
using FoodLoop.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Polly;
using Polly.Registry;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Integrations;

public class LiveAiServiceSmokeTests
{
    [Fact]
    public async Task Run_Live_AI_Service_Smoke_Check()
    {
        // 1. Opt-in Gate
        var runLive = Environment.GetEnvironmentVariable("RUN_LIVE_AI_SERVICE_TESTS") == "true" ||
                      Environment.GetEnvironmentVariable("RUN_EXTERNAL_INTEGRATION_TESTS") == "true";

        if (!runLive)
        {
            // Opt-in test is bypassed in standard CI environments.
            return;
        }

        // 2. Setup Real Client configuration from environment
        var baseUrl = Environment.GetEnvironmentVariable("AI_SERVICE_LIVE_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "http://localhost:8000"; // Fallback to local running instance
        }

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AiService:BaseUrl", baseUrl },
                { "AiService:TimeoutSeconds", "30" }
            })
            .Build();

        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<IConfiguration>(configuration);
        
        var mockCorrelationAccessor = new Mock<ICorrelationIdAccessor>();
        mockCorrelationAccessor.Setup(x => x.GetCorrelationId()).Returns("smoke-test-correlation-id");
        services.AddSingleton(mockCorrelationAccessor.Object);

        services.AddResiliencePipeline<string, HttpResponseMessage>("AiServiceBusinessPipeline", builder =>
        {
            builder.AddTimeout(TimeSpan.FromSeconds(30));
        });

        services.AddHttpClient<IAiServiceClient, AiServiceClient>((sp, client) =>
        {
            client.BaseAddress = new Uri(baseUrl);
        });

        var sp = services.BuildServiceProvider();
        var client = sp.GetRequiredService<IAiServiceClient>();

        // 3. Construct monitoring request for near-expiry, low-velocity product
        var monitoringRequest = new MonitoringRequestDto(
            Product: new("00000000-0000-0000-0000-000000000123", "Banana", "Fruit"),
            Inventory: new(Quantity: 5, OriginalPrice: 20.00m, CurrentPrice: 20.00m, PriceFloor: 14.00m),
            Demand: new(SalesVelocity: 0.1, HistoricalSales: new(2.5)),
            Expiry: new(ExpiresAt: DateTimeOffset.UtcNow.AddHours(12), HoursRemaining: 12.0),
            Location: new(Latitude: 30.06, Longitude: 31.25, StoreId: "00000000-0000-0000-0000-000000000456"),
            StorePolicy: new(StoreId: "00000000-0000-0000-0000-000000000456", OperatingMode: "autonomous"),
            Timestamp: DateTimeOffset.UtcNow
        );

        // 4. Act & Assert - Monitoring Response
        var monitorStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var monitoringResult = await client.AnalyzeMonitoringAsync(monitoringRequest);
        monitorStopwatch.Stop();
        Console.WriteLine($"[Live AI Smoke Test] AnalyzeMonitoringAsync duration: {monitorStopwatch.ElapsedMilliseconds} ms");
        Console.WriteLine($"[Live AI Smoke Test] Real Risk Level: {monitoringResult.RiskLevel}");
        Console.WriteLine($"[Live AI Smoke Test] Real Route: {monitoringResult.Route}");
        Console.WriteLine($"[Live AI Smoke Test] Real Confidence: {monitoringResult.Confidence}");
        Console.WriteLine($"[Live AI Smoke Test] Real Reason: {monitoringResult.Reason}");
        monitorStopwatch.ElapsedMilliseconds.Should().BeGreaterThan(0);
        
        monitoringResult.Should().NotBeNull();
        monitoringResult.RiskLevel.Should().Match(r =>
            r == "LOW" || r == "MEDIUM" || r == "HIGH" || r == "CRITICAL"
        );
        monitoringResult.Confidence.Should().BeInRange(0.0, 1.0);
        monitoringResult.Route.Should().Match(rt => rt == "NO_ACTION" || rt == "PRICING");

        // 5. If routed to pricing, send pricing batch request
        if (monitoringResult.Route == "PRICING")
        {
            var pricingRequest = new PricingBatchRequestDto(
                StoreId: "00000000-0000-0000-0000-000000000456",
                StorePolicy: new("00000000-0000-0000-0000-000000000456", "autonomous"),
                Products: new List<PricingProductRequestDto>
                {
                    new(
                        ProductId: "00000000-0000-0000-0000-000000000123",
                        ProductName: "Banana",
                        Category: "Fruit",
                        Inventory: new(Quantity: 5, OriginalPrice: 20.00m, CurrentPrice: 20.00m, PriceFloor: 14.00m),
                        Demand: new(SalesVelocity: 0.1, HistoricalSales: new(2.5)),
                        Expiry: new(ExpiresAt: DateTimeOffset.UtcNow.AddHours(12), HoursRemaining: 12.0),
                        RiskAssessment: new(monitoringResult.RiskLevel, monitoringResult.Reason, monitoringResult.Confidence)
                    )
                }
            );

            var pricingStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var pricingResult = await client.RecommendPricingAsync(pricingRequest);
            pricingStopwatch.Stop();
            Console.WriteLine($"[Live AI Smoke Test] RecommendPricingAsync duration: {pricingStopwatch.ElapsedMilliseconds} ms");
            pricingStopwatch.ElapsedMilliseconds.Should().BeGreaterThan(0);

            pricingResult.Should().NotBeNull();
            pricingResult.StoreId.Should().Be("00000000-0000-0000-0000-000000000456");
            pricingResult.Decisions.Should().HaveCount(1);

            var decision = pricingResult.Decisions[0];
            Console.WriteLine($"[Live AI Smoke Test] Real Discount Percentage: {decision.DiscountPercentage}%");
            Console.WriteLine($"[Live AI Smoke Test] Real Decision Confidence: {decision.Confidence}");
            Console.WriteLine($"[Live AI Smoke Test] Real Action Requirement: {decision.ActionRequirement}");
            Console.WriteLine($"[Live AI Smoke Test] Real Decision Reason: {decision.Reason}");

            decision.ProductId.Should().Be("00000000-0000-0000-0000-000000000123");
            decision.DiscountPercentage.Should().BeInRange(0.0, 15.0);
            decision.Confidence.Should().BeInRange(0.0, 1.0);
            decision.Reason.Should().NotBeNullOrWhiteSpace();
            decision.Reason.Length.Should().BeLessThanOrEqualTo(500, "reason should be a concise rationale");
            decision.ActionRequirement.Should().Match(req => 
                req == "AUTOMATIC_EXECUTION_ELIGIBLE" || req == "APPROVAL_REQUIRED"
            );
        }
    }
}
