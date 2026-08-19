using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FoodLoop.Application.Common.DTOs.AI;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Exceptions.AI;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Interfaces.AI;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.AiIntegration.Commands;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.AiIntegration.Commands;
using FoodLoop.Infrastructure.Features.Admin.Commands;
using FoodLoop.Infrastructure.Options;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.DependencyInjection;
using FoodLoop.Infrastructure.Integrations.AiService;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Moq.Protected;
using Polly;
using Polly.CircuitBreaker;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.AiIntegration;

[Trait("Category", "Integration")]
public class AiEndToEndIntegrationTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _sqliteConnection;
    private readonly DbContextOptions<ApplicationDbContext> _dbOptions;
    
    // Mocks
    private readonly Mock<IAiServiceClient> _mockAiClient;
    private readonly Mock<ICorrelationIdAccessor> _mockCorrelationAccessor;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    
    private readonly Mock<ILogger<RunMonitoringScanCommandHandler>> _mockMonitoringLogger;
    private readonly Mock<ILogger<RunPricingBatchCommandHandler>> _mockPricingLogger;
    private readonly Mock<ILogger<ApproveAiRecommendationCommandHandler>> _mockApproveLogger;
    private readonly Mock<ILogger<RejectAiRecommendationCommandHandler>> _mockRejectLogger;
    private readonly Mock<ILogger<RunHistoricalIngestionCommandHandler>> _mockIngestionLogger;
    private readonly Mock<ILogger<RequestHistoricalEpisodeCorrectionCommandHandler>> _mockCorrectionLogger;
    
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly string _correlationId = "e2e-correlation-id-999";

    public AiEndToEndIntegrationTests()
    {
        _sqliteConnection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        _sqliteConnection.Open();

        _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        // Ensure tables are built
        using (var setupContext = new TestApplicationDbContextForE2E(_dbOptions))
        {
            setupContext.Database.EnsureCreated();
        }

        _mockAiClient = new Mock<IAiServiceClient>();
        _mockCorrelationAccessor = new Mock<ICorrelationIdAccessor>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        
        _mockMonitoringLogger = new Mock<ILogger<RunMonitoringScanCommandHandler>>();
        _mockPricingLogger = new Mock<ILogger<RunPricingBatchCommandHandler>>();
        _mockApproveLogger = new Mock<ILogger<ApproveAiRecommendationCommandHandler>>();
        _mockRejectLogger = new Mock<ILogger<RejectAiRecommendationCommandHandler>>();
        _mockIngestionLogger = new Mock<ILogger<RunHistoricalIngestionCommandHandler>>();
        _mockCorrectionLogger = new Mock<ILogger<RequestHistoricalEpisodeCorrectionCommandHandler>>();
        
        _fakeTimeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero));
        _mockCorrelationAccessor.Setup(x => x.GetCorrelationId()).Returns(_correlationId);
    }

    public void Dispose()
    {
        _sqliteConnection.Dispose();
    }

    private TestApplicationDbContextForE2E CreateDbContext()
    {
        return new TestApplicationDbContextForE2E(_dbOptions);
    }

    private RunMonitoringScanCommandHandler CreateMonitoringHandler(IApplicationDbContext dbContext, MonitoringScannerOptions? options = null)
    {
        var opt = options ?? new MonitoringScannerOptions
        {
            IntervalMinutes = 60,
            ExpirationThresholdDays = 3,
            VelocityThresholdMultiplier = 0.8
        };
        var mockOptions = new Mock<IOptions<MonitoringScannerOptions>>();
        mockOptions.Setup(x => x.Value).Returns(opt);

        return new RunMonitoringScanCommandHandler(
            dbContext,
            _mockAiClient.Object,
            _mockCorrelationAccessor.Object,
            mockOptions.Object,
            _fakeTimeProvider,
            _mockMonitoringLogger.Object
        );
    }

    private RunPricingBatchCommandHandler CreatePricingHandler(IApplicationDbContext dbContext, int maxBatchSize = 50)
    {
        var opt = new AiServiceOptions
        {
            BaseUrl = "http://localhost:8000",
            TimeoutSeconds = 15,
            MaxPricingBatchSize = maxBatchSize
        };
        var mockOptions = new Mock<IOptions<AiServiceOptions>>();
        mockOptions.Setup(x => x.Value).Returns(opt);

        return new RunPricingBatchCommandHandler(
            dbContext,
            _mockAiClient.Object,
            _mockCorrelationAccessor.Object,
            _fakeTimeProvider,
            _mockPricingLogger.Object,
            mockOptions.Object
        );
    }

    private ApproveAiRecommendationCommandHandler CreateApproveHandler(IApplicationDbContext dbContext)
    {
        return new ApproveAiRecommendationCommandHandler(
            dbContext,
            _fakeTimeProvider,
            _mockApproveLogger.Object
        );
    }

    private RejectAiRecommendationCommandHandler CreateRejectHandler(IApplicationDbContext dbContext)
    {
        return new RejectAiRecommendationCommandHandler(
            dbContext,
            _mockRejectLogger.Object
        );
    }

    private RunHistoricalIngestionCommandHandler CreateIngestionHandler(IApplicationDbContext dbContext, int batchSize = 100)
    {
        var opt = new HistoricalIngestionOptions
        {
            IntervalMinutes = 60,
            BatchSize = batchSize
        };
        var mockOptions = new Mock<IOptions<HistoricalIngestionOptions>>();
        mockOptions.Setup(x => x.Value).Returns(opt);

        return new RunHistoricalIngestionCommandHandler(
            dbContext,
            _mockAiClient.Object,
            _mockCorrelationAccessor.Object,
            _fakeTimeProvider,
            mockOptions.Object,
            _mockIngestionLogger.Object
        );
    }

    private RequestHistoricalEpisodeCorrectionCommandHandler CreateCorrectionHandler(ApplicationDbContext dbContext)
    {
        return new RequestHistoricalEpisodeCorrectionCommandHandler(
            dbContext,
            _mockCurrentUserService.Object,
            _mockCorrectionLogger.Object,
            _mockCorrelationAccessor.Object
        );
    }

    private ServiceProvider BuildResilienceServiceProvider(HttpMessageHandler handler, string baseUrl = "http://localhost:8000", TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AiService:BaseUrl", baseUrl },
                { "AiService:TimeoutSeconds", "10" }
            })
            .Build();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(_mockCorrelationAccessor.Object);
        services.AddTransient<CorrelationIdDelegatingHandler>();

        services.AddOptions<AiServiceOptions>()
            .Bind(configuration.GetSection(AiServiceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddResiliencePipeline<string, HttpResponseMessage>("AiServiceBusinessPipeline", builder =>
        {
            if (timeProvider != null)
            {
                builder.TimeProvider = timeProvider;
            }

            builder.AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.Zero, 
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<Polly.Timeout.TimeoutRejectedException>()
                    .HandleResult(response => (int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
            });

            builder.AddTimeout(new Polly.Timeout.TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(2)
            });

            builder.AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.6,
                SamplingDuration = TimeSpan.FromSeconds(10),
                MinimumThroughput = 3,
                BreakDuration = TimeSpan.FromMinutes(60),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<Polly.Timeout.TimeoutRejectedException>()
                    .HandleResult(response => (int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
            });
        });

        services.AddResiliencePipeline<string, HttpResponseMessage>("AiServiceHealthPipeline", builder =>
        {
            if (timeProvider != null)
            {
                builder.TimeProvider = timeProvider;
            }

            builder.AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 1,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(response => (int)response.StatusCode >= 500)
            });

            builder.AddTimeout(new Polly.Timeout.TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromMilliseconds(100)
            });
        });

        services.AddHttpClient<IAiServiceClient, AiServiceClient>((sp, client) =>
        {
            client.BaseAddress = new Uri(baseUrl);
        })
        .ConfigurePrimaryHttpMessageHandler(() => handler)
        .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

        return services.BuildServiceProvider();
    }

    private async Task<(Organization, Category, Product)> SeedBaseStoreDataAsync(IApplicationDbContext dbContext, AiOperatingMode mode = AiOperatingMode.Assisted, Guid? merchantUserId = null)
    {
        var ownerId = merchantUserId ?? Guid.NewGuid();

        var user = new FoodLoop.Infrastructure.Identity.ApplicationUser
        {
            Id = ownerId,
            UserName = $"merchant-{ownerId}@test.com",
            Email = $"merchant-{ownerId}@test.com"
        };
        
        if (dbContext is ApplicationDbContext appDb)
        {
            appDb.Users.Add(user);
        }

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "E2E Test Store",
            AiOperatingMode = mode,
            OwnerId = ownerId,
            VerificationStatus = VerificationStatus.Verified
        };

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Produce"
        };

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Apples",
            OriginalPrice = 10m,
            DiscountedPrice = 10m,
            QuantityAvailable = 10,
            ExpirationDate = DateOnly.FromDateTime(_fakeTimeProvider.GetUtcNow().DateTime).AddDays(5),
            Status = ProductStatus.Active,
            Organization = org,
            OrganizationId = org.Id,
            Category = category,
            CategoryId = category.Id
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        
        await dbContext.SaveChangesAsync();
        return (org, category, product);
    }

    private async Task SeedSalesHistoryAsync(IApplicationDbContext dbContext, Guid productId, int historicalCount, int recentCount)
    {
        var today = _fakeTimeProvider.GetUtcNow();
        var ordersToUpdate = new List<(Guid OrderId, DateTimeOffset CreatedAt)>();

        // Historical orders (e.g., 20 days ago)
        for (int i = 0; i < historicalCount; i++)
        {
            var orderId = Guid.NewGuid();
            var orderCreatedAt = today.AddDays(-20);
            ordersToUpdate.Add((orderId, orderCreatedAt));

            var order = new Order
            {
                Id = orderId,
                PaymentStatus = PaymentStatus.Paid,
                CreatedAt = orderCreatedAt
            };
            var item = new OrderItem
            {
                OrderId = order.Id,
                ProductId = productId,
                Quantity = 1,
                UnitPrice = 10m
            };
            order.Items.Add(item);
            dbContext.Orders.Add(order);
            dbContext.OrderItems.Add(item);
        }

        // Recent orders (e.g., last 24 hours)
        for (int i = 0; i < recentCount; i++)
        {
            var orderId = Guid.NewGuid();
            var orderCreatedAt = today.AddHours(-12);
            ordersToUpdate.Add((orderId, orderCreatedAt));

            var order = new Order
            {
                Id = orderId,
                PaymentStatus = PaymentStatus.Paid,
                CreatedAt = orderCreatedAt
            };
            var item = new OrderItem
            {
                OrderId = order.Id,
                ProductId = productId,
                Quantity = 1,
                UnitPrice = 10m
            };
            order.Items.Add(item);
            dbContext.Orders.Add(order);
            dbContext.OrderItems.Add(item);
        }

        await dbContext.SaveChangesAsync();

        if (dbContext is DbContext efDb)
        {
            var converter = new DateTimeOffsetToBinaryConverter();
            foreach (var item in ordersToUpdate)
            {
                var binaryVal = converter.ConvertToProvider(item.CreatedAt);
                await efDb.Database.ExecuteSqlRawAsync(
                    "UPDATE Orders SET CreatedAt = {0} WHERE Id = {1}",
                    binaryVal, item.OrderId);
            }
        }
    }

    // ==========================================
    // VECTOR 1: INVENTORY MONITORING SCAN
    // ==========================================

    [Fact]
    public async Task Monitoring_LowRisk_ShouldRouteNoAction_AndNotStagePricing()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var (_, _, product) = await SeedBaseStoreDataAsync(dbContext);
        
        // Product has distant expiration (>7 days)
        product.ExpirationDate = DateOnly.FromDateTime(_fakeTimeProvider.GetUtcNow().DateTime).AddDays(10);
        await dbContext.SaveChangesAsync();

        // Seed 5 historical orders and 0 recent orders to trigger the velocity criteria scan (salesVelocity < historicalAvg * multiplier)
        await SeedSalesHistoryAsync(dbContext, product.Id, 5, 0);

        _mockAiClient.Setup(x => x.AnalyzeMonitoringAsync(It.IsAny<MonitoringRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MonitoringResponseDto("NO_ACTION", "LOW", "Distant expiration date.", 0.99));

        var handler = CreateMonitoringHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunMonitoringScanCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        
        var assessment = await dbContext.AiRiskAssessments.FirstOrDefaultAsync(a => a.ProductId == product.Id);
        assessment.Should().NotBeNull();
        assessment!.Route.Should().Be(AiRoute.NO_ACTION);
        assessment.RiskLevel.Should().Be(AiRiskLevel.LOW);
        assessment.IsPricingStaged.Should().BeFalse();
    }

    [Fact]
    public async Task Monitoring_HighRisk_ShouldRoutePricing_AndStageForBatch()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var (_, _, product) = await SeedBaseStoreDataAsync(dbContext);
        
        // Product has < 48 hours to expiry
        product.ExpirationDate = DateOnly.FromDateTime(_fakeTimeProvider.GetUtcNow().DateTime).AddDays(1);
        await dbContext.SaveChangesAsync();

        _mockAiClient.Setup(x => x.AnalyzeMonitoringAsync(It.IsAny<MonitoringRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MonitoringResponseDto("PRICING", "HIGH", "Nearing expiration date.", 0.95));

        var handler = CreateMonitoringHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunMonitoringScanCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        
        var assessment = await dbContext.AiRiskAssessments.FirstOrDefaultAsync(a => a.ProductId == product.Id);
        assessment.Should().NotBeNull();
        assessment!.Route.Should().Be(AiRoute.PRICING);
        assessment.RiskLevel.Should().Be(AiRiskLevel.HIGH);
        assessment.IsPricingStaged.Should().BeTrue();
    }

    [Fact]
    public async Task Monitoring_ZeroVelocityZeroBaseline_ShouldHandleGracefully()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var (_, _, product) = await SeedBaseStoreDataAsync(dbContext);
        
        product.QuantityAvailable = 5;
        product.ExpirationDate = DateOnly.FromDateTime(_fakeTimeProvider.GetUtcNow().DateTime).AddDays(1);
        await dbContext.SaveChangesAsync();

        // 0 orders exist in DB -> velocity and historical baseline are calculated as 0
        _mockAiClient.Setup(x => x.AnalyzeMonitoringAsync(It.IsAny<MonitoringRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MonitoringResponseDto("PRICING", "HIGH", "No demand, high risk.", 0.90));

        var handler = CreateMonitoringHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunMonitoringScanCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var assessment = await dbContext.AiRiskAssessments.FirstOrDefaultAsync(a => a.ProductId == product.Id);
        assessment.Should().NotBeNull();
        assessment!.IsPricingStaged.Should().BeTrue();
    }

    [Fact]
    public async Task Monitoring_CorrelationId_ShouldPropagateAcrossRequest()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var (_, _, product) = await SeedBaseStoreDataAsync(dbContext);
        
        product.ExpirationDate = DateOnly.FromDateTime(_fakeTimeProvider.GetUtcNow().DateTime).AddDays(1);
        await dbContext.SaveChangesAsync();

        _mockAiClient.Setup(x => x.AnalyzeMonitoringAsync(It.IsAny<MonitoringRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MonitoringResponseDto("PRICING", "HIGH", "Correlation test", 0.90));

        var handler = CreateMonitoringHandler(dbContext);

        // Act
        await handler.Handle(new RunMonitoringScanCommand(), CancellationToken.None);

        // Assert
        var assessment = await dbContext.AiRiskAssessments.FirstOrDefaultAsync(a => a.ProductId == product.Id);
        assessment.Should().NotBeNull();
        assessment!.CorrelationId.Should().Be(_correlationId);
    }

    // ==========================================
    // VECTOR 2: BATCH PRICING RECOMMENDATION
    // ==========================================

    [Fact]
    public async Task PricingBatch_AssistedStore_ShouldCreatePendingRecommendations()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var (org, _, product) = await SeedBaseStoreDataAsync(dbContext, AiOperatingMode.Assisted);

        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.95, _correlationId, isPricingStaged: true)
        {
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
        };
        dbContext.AiRiskAssessments.Add(risk);
        await dbContext.SaveChangesAsync();

        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.IsAny<PricingBatchRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingBatchResponseDto(org.Id.ToString(), new List<PricingDecisionDto>
            {
                new PricingDecisionDto(product.Id.ToString(), 10.0, "Recommend 10% discount", 0.92, "APPROVAL_REQUIRED", "Store in Assisted mode")
            }));

        var handler = CreatePricingHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        
        var recommendation = await dbContext.AiPricingRecommendations.FirstOrDefaultAsync(r => r.ProductId == product.Id);
        recommendation.Should().NotBeNull();
        recommendation!.Status.Should().Be(AiRecommendationStatus.Pending);
        recommendation.ActionRequirement.Should().Be(AiActionRequirement.APPROVAL_REQUIRED);
        recommendation.SnapshotOriginalPrice.Should().Be(product.OriginalPrice);
        recommendation.SnapshotQuantityAvailable.Should().Be(product.QuantityAvailable);
        
        // Product price remains unchanged
        var dbProduct = await dbContext.Products.FindAsync(product.Id);
        dbProduct!.DiscountedPrice.Should().Be(product.OriginalPrice);
    }

    [Fact]
    public async Task PricingBatch_AutonomousStore_ShouldAutoMutatePriceAndAuditHistory()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var (org, _, product) = await SeedBaseStoreDataAsync(dbContext, AiOperatingMode.Autonomous);

        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.95, _correlationId, isPricingStaged: true)
        {
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
        };
        dbContext.AiRiskAssessments.Add(risk);
        await dbContext.SaveChangesAsync();

        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.IsAny<PricingBatchRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingBatchResponseDto(org.Id.ToString(), new List<PricingDecisionDto>
            {
                new PricingDecisionDto(product.Id.ToString(), 5.0, "Recommend 5% discount", 0.95, "AUTOMATIC_EXECUTION_ELIGIBLE", "Store in Autonomous mode")
            }));

        var handler = CreatePricingHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        var dbProduct = await dbContext.Products.FindAsync(product.Id);
        dbProduct!.DiscountedPrice.Should().Be(9.5m); // 10m - 5% = 9.5m

        var history = await dbContext.PriceHistories.FirstOrDefaultAsync(h => h.ProductId == product.Id);
        history.Should().NotBeNull();
        history!.NewDiscountedPrice.Should().Be(9.5m);
        history.ChangeReason.Should().Contain("AI Autonomous Pricing");

        var rec = await dbContext.AiPricingRecommendations.FirstOrDefaultAsync(r => r.ProductId == product.Id);
        rec.Should().NotBeNull();
        rec!.Status.Should().Be(AiRecommendationStatus.AutoExecuted);
    }

    [Fact]
    public async Task PricingBatch_PriceFloorViolation_ShouldRejectImmediately()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var (org, _, product) = await SeedBaseStoreDataAsync(dbContext, AiOperatingMode.Autonomous);

        var settings = await dbContext.SystemSettings.FirstAsync();
        settings.DefaultPriceFloorPolicy = PriceFloorPolicy.DynamicAi;
        dbContext.SystemSettings.Update(settings);

        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.95, _correlationId, isPricingStaged: true)
        {
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
        };
        dbContext.AiRiskAssessments.Add(risk);
        await dbContext.SaveChangesAsync();

        // 15% discount makes proposed price 8.5m, which violates the 9.0m price floor
        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.IsAny<PricingBatchRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingBatchResponseDto(org.Id.ToString(), new List<PricingDecisionDto>
            {
                new PricingDecisionDto(product.Id.ToString(), 15.0, "Recommend 15% discount", 0.95, "AUTOMATIC_EXECUTION_ELIGIBLE", "Store in Autonomous mode")
            }));

        var handler = CreatePricingHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        var rec = await dbContext.AiPricingRecommendations.FirstOrDefaultAsync(r => r.ProductId == product.Id);
        rec.Should().NotBeNull();
        rec!.Status.Should().Be(AiRecommendationStatus.Rejected);
        rec.ActionReason.Should().Contain("Price Floor Violation");

        var dbProduct = await dbContext.Products.FindAsync(product.Id);
        dbProduct!.DiscountedPrice.Should().Be(10m); // Untouched

        var histories = await dbContext.PriceHistories.AnyAsync(h => h.ProductId == product.Id);
        histories.Should().BeFalse();
    }

    [Fact]
    public async Task PricingBatch_BatchChunking_ShouldSegmentLargeRequests()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Large Store", AiOperatingMode = AiOperatingMode.Assisted, VerificationStatus = VerificationStatus.Verified };
        var category = new Category { Id = Guid.NewGuid(), Name = "Large Cat" };
        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);

        int totalProducts = 55;
        var decisionsList = new List<PricingDecisionDto>();

        for (int i = 0; i < totalProducts; i++)
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Title = $"Product-{i}",
                OriginalPrice = 10m,
                DiscountedPrice = 10m,
                QuantityAvailable = 5,
                ExpirationDate = DateOnly.FromDateTime(_fakeTimeProvider.GetUtcNow().DateTime).AddDays(2),
                Status = ProductStatus.Active,
                Organization = org,
                Category = category
            };
            dbContext.Products.Add(product);

            var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Staged", 0.9, _correlationId, isPricingStaged: true)
            {
                SnapshotOriginalPrice = product.OriginalPrice,
                SnapshotQuantityAvailable = product.QuantityAvailable,
                SnapshotProductStatus = product.Status
            };
            dbContext.AiRiskAssessments.Add(risk);

            decisionsList.Add(new PricingDecisionDto(product.Id.ToString(), 10.0, "Discounted", 0.90, "APPROVAL_REQUIRED", "Assisted Mode"));
        }
        await dbContext.SaveChangesAsync();

        // Setup AI Client Mock to return results for chunks
        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.IsAny<PricingBatchRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PricingBatchRequestDto req, CancellationToken ct) =>
            {
                var chunkDecisions = decisionsList.Where(d => req.Products.Any(p => p.ProductId == d.ProductId)).ToList();
                return new PricingBatchResponseDto(org.Id.ToString(), chunkDecisions);
            });

        var handler = CreatePricingHandler(dbContext, maxBatchSize: 50);

        // Act
        var result = await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        
        // Assert that client was called exactly twice (Chunk 1 of 50, Chunk 2 of 5)
        _mockAiClient.Verify(x => x.RecommendPricingAsync(It.IsAny<PricingBatchRequestDto>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task PricingBatch_DuplicateAssessments_ShouldDeStageOlderRecords()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var (org, _, product) = await SeedBaseStoreDataAsync(dbContext, AiOperatingMode.Assisted);

        // Two risk assessments staged for same product
        var riskOld = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Old", 0.90, _correlationId, isPricingStaged: true)
        {
            CreatedAt = _fakeTimeProvider.GetUtcNow().AddHours(-2),
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
        };
        var riskNew = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "New", 0.95, _correlationId, isPricingStaged: true)
        {
            CreatedAt = _fakeTimeProvider.GetUtcNow().AddHours(-1),
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
        };
        var oldCreatedAt = riskOld.CreatedAt;
        var newCreatedAt = riskNew.CreatedAt;

        dbContext.AiRiskAssessments.AddRange(riskOld, riskNew);
        await dbContext.SaveChangesAsync();

        // Update CreatedAt directly using raw SQL to bypass the EF Core automatic timestamp override
        var converter = new DateTimeOffsetToBinaryConverter();
        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE AiRiskAssessments SET CreatedAt = {0} WHERE Id = {1}",
            converter.ConvertToProvider(oldCreatedAt)!, riskOld.Id);
        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE AiRiskAssessments SET CreatedAt = {0} WHERE Id = {1}",
            converter.ConvertToProvider(newCreatedAt)!, riskNew.Id);

        dbContext.ChangeTracker.Clear();

        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.IsAny<PricingBatchRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingBatchResponseDto(org.Id.ToString(), new List<PricingDecisionDto>
            {
                new PricingDecisionDto(product.Id.ToString(), 10.0, "Decide", 0.95, "APPROVAL_REQUIRED", "Assisted")
            }));

        var handler = CreatePricingHandler(dbContext);

        // Act
        await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        // Assert
        var oldDb = await dbContext.AiRiskAssessments.FindAsync(riskOld.Id);
        var newDb = await dbContext.AiRiskAssessments.FindAsync(riskNew.Id);
        
        oldDb!.IsPricingStaged.Should().BeFalse(); // Destaged
        newDb!.IsPricingStaged.Should().BeTrue(); 
    }

    // ==========================================
    // VECTOR 3: MERCHANT APPROVAL & REJECTION
    // ==========================================

    [Fact]
    public async Task MerchantApproval_ValidPending_ShouldMutatePriceAndRecordHistory()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var merchantUserId = Guid.NewGuid();
        var (org, _, product) = await SeedBaseStoreDataAsync(dbContext, AiOperatingMode.Assisted, merchantUserId);

        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Expiry", 0.95, _correlationId, isPricingStaged: true);
        dbContext.AiRiskAssessments.Add(risk);

        var recommendation = new AiPricingRecommendation(
            product.Id, org.Id, 10.0m, "Reason", 0.92,
            AiActionRequirement.APPROVAL_REQUIRED, "Reason", _correlationId,
            AiRecommendationStatus.Pending, risk.Id
        )
        {
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
        };
        dbContext.AiPricingRecommendations.Add(recommendation);
        await dbContext.SaveChangesAsync();

        var handler = CreateApproveHandler(dbContext);

        // Act
        var result = await handler.Handle(new ApproveAiRecommendationCommand(merchantUserId, recommendation.Id), CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        // Assert
        result.Success.Should().BeTrue();

        var dbProduct = await dbContext.Products.FindAsync(product.Id);
        dbProduct!.DiscountedPrice.Should().Be(9.0m); // 10m - 10% = 9.0m

        var history = await dbContext.PriceHistories.FirstOrDefaultAsync(h => h.ProductId == product.Id);
        history.Should().NotBeNull();
        history!.NewDiscountedPrice.Should().Be(9.0m);

        var rec = await dbContext.AiPricingRecommendations.FindAsync(recommendation.Id);
        rec!.Status.Should().Be(AiRecommendationStatus.Approved);
        rec.ExecutedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MerchantApproval_StaleState_ShouldRejectAsStale()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var merchantUserId = Guid.NewGuid();
        var (org, _, product) = await SeedBaseStoreDataAsync(dbContext, AiOperatingMode.Assisted, merchantUserId);

        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Expiry", 0.95, _correlationId, isPricingStaged: true);
        dbContext.AiRiskAssessments.Add(risk);

        var recommendation = new AiPricingRecommendation(
            product.Id, org.Id, 10.0m, "Reason", 0.92,
            AiActionRequirement.APPROVAL_REQUIRED, "Reason", _correlationId,
            AiRecommendationStatus.Pending, risk.Id
        )
        {
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
        };
        dbContext.AiPricingRecommendations.Add(recommendation);
        await dbContext.SaveChangesAsync();

        // Mutate product state between recommendation creation and approval
        product.QuantityAvailable = 3; 
        await dbContext.SaveChangesAsync();

        var handler = CreateApproveHandler(dbContext);

        // Act
        var result = await handler.Handle(new ApproveAiRecommendationCommand(merchantUserId, recommendation.Id), CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Stale Recommendation");

        var rec = await dbContext.AiPricingRecommendations.FindAsync(recommendation.Id);
        rec!.Status.Should().Be(AiRecommendationStatus.Rejected);
        rec.ActionReason.Should().Be("Stale Recommendation - Product State Changed");

        var dbProduct = await dbContext.Products.FindAsync(product.Id);
        dbProduct!.DiscountedPrice.Should().Be(10m); // Untouched
    }

    [Fact]
    public async Task MerchantApproval_DoubleApproval_ShouldThrowConflictException()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var merchantUserId = Guid.NewGuid();
        var (org, _, product) = await SeedBaseStoreDataAsync(dbContext, AiOperatingMode.Assisted, merchantUserId);

        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Expiry", 0.95, _correlationId, isPricingStaged: true);
        dbContext.AiRiskAssessments.Add(risk);

        var recommendation = new AiPricingRecommendation(
            product.Id, org.Id, 10.0m, "Reason", 0.92,
            AiActionRequirement.APPROVAL_REQUIRED, "Reason", _correlationId,
            AiRecommendationStatus.Pending, risk.Id
        )
        {
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
        };
        dbContext.AiPricingRecommendations.Add(recommendation);
        await dbContext.SaveChangesAsync();

        var handler = CreateApproveHandler(dbContext);

        // Act - Call 1 (succeeds)
        var result1 = await handler.Handle(new ApproveAiRecommendationCommand(merchantUserId, recommendation.Id), CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        result1.Success.Should().BeTrue();

        // Act - Call 2 (fails/throws ConflictException)
        Func<Task> act2 = async () =>
        {
            dbContext.ChangeTracker.Clear();
            await handler.Handle(new ApproveAiRecommendationCommand(merchantUserId, recommendation.Id), CancellationToken.None);
        };
        await act2.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task MerchantRejection_ValidPending_ShouldTransitionToRejected()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var merchantUserId = Guid.NewGuid();
        var (org, _, product) = await SeedBaseStoreDataAsync(dbContext, AiOperatingMode.Assisted, merchantUserId);

        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Expiry", 0.95, _correlationId, isPricingStaged: true);
        dbContext.AiRiskAssessments.Add(risk);

        var recommendation = new AiPricingRecommendation(
            product.Id, org.Id, 10.0m, "Reason", 0.92,
            AiActionRequirement.APPROVAL_REQUIRED, "Reason", _correlationId,
            AiRecommendationStatus.Pending, risk.Id
        )
        {
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
        };
        dbContext.AiPricingRecommendations.Add(recommendation);
        await dbContext.SaveChangesAsync();

        var handler = CreateRejectHandler(dbContext);

        // Act
        var result = await handler.Handle(new RejectAiRecommendationCommand(merchantUserId, recommendation.Id, "Too cheap"), CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        // Assert
        result.Success.Should().BeTrue();

        var rec = await dbContext.AiPricingRecommendations.FindAsync(recommendation.Id);
        rec!.Status.Should().Be(AiRecommendationStatus.Rejected);
        rec.ActionReason.Should().Be("Too cheap");

        var dbProduct = await dbContext.Products.FindAsync(product.Id);
        dbProduct!.DiscountedPrice.Should().Be(10m); // Untouched
    }

    // ==========================================
    // VECTOR 4: HISTORICAL EPISODE INGESTION
    // ==========================================

    [Fact]
    public async Task HistoricalIngestion_FinalizedEpisodes_ShouldBatchAndStampAtomically()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var (org, _, product) = await SeedBaseStoreDataAsync(dbContext);
        
        product.QuantityAvailable = 0;
        await dbContext.SaveChangesAsync();

        var eventId = $"ep-{product.Id}-nodisc";
        
        _mockAiClient.Setup(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HistoricalIngestionResponseDto(1, 1, 0, new List<string> { eventId }));

        var handler = CreateIngestionHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunHistoricalIngestionCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        var episode = await dbContext.ProductPricingEpisodes.FirstOrDefaultAsync(pe => pe.ProductId == product.Id && pe.EventId == eventId);
        episode.Should().NotBeNull();
        episode!.IngestedAt.Should().NotBeNull();
        episode.IngestionCorrelationId.Should().Be(_correlationId);
    }

    [Fact]
    public async Task HistoricalIngestion_Idempotency_ShouldSkipAlreadyIngested()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var (org, _, product) = await SeedBaseStoreDataAsync(dbContext);
        
        product.QuantityAvailable = 0;
        await dbContext.SaveChangesAsync();

        var eventId = $"ep-{product.Id}-nodisc";

        var episode = new ProductPricingEpisode
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            EventId = eventId,
            RecordedAt = _fakeTimeProvider.GetUtcNow().AddHours(-1),
            IngestedAt = _fakeTimeProvider.GetUtcNow().AddHours(-1),
            IngestionCorrelationId = "existing-corr",
            Outcome = "UNSOLD"
        };
        dbContext.ProductPricingEpisodes.Add(episode);
        await dbContext.SaveChangesAsync();

        _mockAiClient.Setup(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Should not be called"));

        var handler = CreateIngestionHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunHistoricalIngestionCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task HistoricalIngestion_AdminCorrection_ShouldRequeueEpisode()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var adminId = Guid.NewGuid();
        
        _mockCurrentUserService.Setup(x => x.UserId).Returns(adminId);
        _mockCurrentUserService.Setup(x => x.IsInRole(AppRole.Admin)).Returns(true);

        var (org, _, product) = await SeedBaseStoreDataAsync(dbContext);
        
        var episode = new ProductPricingEpisode
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            EventId = $"ep-{product.Id}-nodisc",
            RecordedAt = _fakeTimeProvider.GetUtcNow().AddHours(-1),
            IngestedAt = _fakeTimeProvider.GetUtcNow().AddHours(-1),
            IngestionCorrelationId = "corr-old",
            Outcome = "UNSOLD",
            DiscountPercentage = 5.0,
            SellThroughRate = 0.2
        };
        dbContext.ProductPricingEpisodes.Add(episode);
        await dbContext.SaveChangesAsync();

        var handler = CreateCorrectionHandler(dbContext);
        var command = new RequestHistoricalEpisodeCorrectionCommand(
            RowId: episode.Id,
            EventId: null,
            Reason: "Correction requested by E2E suite",
            CorrectedDiscountPercentage: 10.0,
            CorrectedSellThroughRate: 0.8,
            CorrectedOutcome: "SOLD_OUT"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        var dbEpisode = await dbContext.ProductPricingEpisodes.FindAsync(episode.Id);
        dbEpisode!.IngestedAt.Should().BeNull(); // Reset
        dbEpisode.IngestionCorrelationId.Should().BeNull();
        dbEpisode.DiscountPercentage.Should().Be(10.0);
        dbEpisode.SellThroughRate.Should().Be(0.8);
        dbEpisode.Outcome.Should().Be("SOLD_OUT");
    }

    // ==========================================
    // VECTOR 5: CONTRACT INVARIANTS & RESILIENCE
    // ==========================================

    [Fact]
    public async Task Contract_DiscountExceedsCeiling_ShouldThrowContractException()
    {
        // Arrange
        var responseObj = new PricingBatchResponseDto("store-id-123", new List<PricingDecisionDto>
        {
            new PricingDecisionDto("prod-1", 18.5, "Exceeds max discount limit", 0.95, "APPROVAL_REQUIRED", "Reason")
        });
        
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(responseObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }))
        };

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(mockResponse);

        using var sp = BuildResilienceServiceProvider(mockHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new PricingBatchRequestDto("store-id-123", new PricingStorePolicyDto("store-id-123", "assisted"), new List<PricingProductRequestDto>
        {
            new PricingProductRequestDto(
                "prod-1", "Apples", "Produce",
                new PricingInventoryDto(10, 10m, 10m, 9m),
                new PricingDemandDto(1.0, new PricingHistoricalSalesDto(1.0)),
                new PricingExpiryDto(DateTimeOffset.UtcNow, 24.0),
                new PricingRiskAssessmentDto("HIGH", "Expiry", 0.90)
            )
        });

        // Act & Assert
        Func<Task> act = async () => await client.RecommendPricingAsync(request, CancellationToken.None);
        await act.Should().ThrowAsync<AiServiceContractException>()
            .WithMessage("*DiscountPercentage value 18.5 is out of the allowed [0.0, 15.0] range.*");
    }

    [Fact]
    public async Task Contract_ConfidenceOutOfBounds_ShouldThrowContractException()
    {
        // Arrange
        var responseObj = new PricingBatchResponseDto("store-id-123", new List<PricingDecisionDto>
        {
            new PricingDecisionDto("prod-1", 10.0, "Decide", 1.2, "APPROVAL_REQUIRED", "Reason")
        });

        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(responseObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }))
        };

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(mockResponse);

        using var sp = BuildResilienceServiceProvider(mockHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new PricingBatchRequestDto("store-id-123", new PricingStorePolicyDto("store-id-123", "assisted"), new List<PricingProductRequestDto>
        {
            new PricingProductRequestDto(
                "prod-1", "Apples", "Produce",
                new PricingInventoryDto(10, 10m, 10m, 9m),
                new PricingDemandDto(1.0, new PricingHistoricalSalesDto(1.0)),
                new PricingExpiryDto(DateTimeOffset.UtcNow, 24.0),
                new PricingRiskAssessmentDto("HIGH", "Expiry", 0.90)
            )
        });

        // Act & Assert
        Func<Task> act = async () => await client.RecommendPricingAsync(request, CancellationToken.None);
        await act.Should().ThrowAsync<AiServiceContractException>()
            .WithMessage("*Confidence value 1.2 is out of the allowed [0.0, 1.0] range.*");
    }

    [Fact]
    public async Task Resilience_Ai500Error_ShouldRetryAndTripCircuitBreaker()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.InternalServerError)); 

        var fakeTime = new FakeTimeProvider();
        using var sp = BuildResilienceServiceProvider(mockHandler.Object, timeProvider: fakeTime);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new PricingBatchRequestDto("store-id-123", new PricingStorePolicyDto("store-id-123", "assisted"), new List<PricingProductRequestDto>
        {
            new PricingProductRequestDto(
                "prod-1", "Apples", "Produce",
                new PricingInventoryDto(10, 10m, 10m, 9m),
                new PricingDemandDto(1.0, new PricingHistoricalSalesDto(1.0)),
                new PricingExpiryDto(DateTimeOffset.UtcNow, 24.0),
                new PricingRiskAssessmentDto("HIGH", "Expiry", 0.90)
            )
        });

        // Act & Assert
        // Request 1: should retry internally, trip breaker on 3rd attempt, and fail
        Func<Task> act1 = async () => await client.RecommendPricingAsync(request, CancellationToken.None);
        var ex1 = await act1.Should().ThrowAsync<AiServiceUnavailableException>();
        ex1.WithInnerException<BrokenCircuitException>();
        
        mockHandler.Protected().Verify("SendAsync", Times.Exactly(3), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());

        // Circuit breaker is now open. Subsequent call should throw BrokenCircuitException immediately without calling downstream handler
        Func<Task> actFast = async () => await client.RecommendPricingAsync(request, CancellationToken.None);
        var exception = await actFast.Should().ThrowAsync<AiServiceUnavailableException>();
        exception.WithInnerException<BrokenCircuitException>();

        // Verify that the second (fast-fail) call did not invoke SendAsync again (total invocations remain exactly 3)
        mockHandler.Protected().Verify("SendAsync", Times.Exactly(3), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Resilience_Validation422Error_ShouldMapToValidationException()
    {
        // Arrange
        var mockResponse = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent(@"{ ""detail"": ""Validation error details"" }")
        };

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(mockResponse);

        using var sp = BuildResilienceServiceProvider(mockHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new PricingBatchRequestDto("store-id-123", new PricingStorePolicyDto("store-id-123", "assisted"), new List<PricingProductRequestDto>
        {
            new PricingProductRequestDto(
                "prod-1", "Apples", "Produce",
                new PricingInventoryDto(10, 10m, 10m, 9m),
                new PricingDemandDto(1.0, new PricingHistoricalSalesDto(1.0)),
                new PricingExpiryDto(DateTimeOffset.UtcNow, 24.0),
                new PricingRiskAssessmentDto("HIGH", "Expiry", 0.90)
            )
        });

        // Act & Assert
        Func<Task> act = async () => await client.RecommendPricingAsync(request, CancellationToken.None);
        await act.Should().ThrowAsync<AiServiceValidationException>()
            .WithMessage("*AI Service returned HTTP 422 Unprocessable Entity*");
            
        mockHandler.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }
}

public class TestApplicationDbContextForE2E : ApplicationDbContext
{
    public TestApplicationDbContextForE2E(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }



    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Add DateTimeOffset converter for SQLite compatibility in tests
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var properties = entityType.GetProperties()
                .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?));
            foreach (var property in properties)
            {
                property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
            }
        }
    }
}
