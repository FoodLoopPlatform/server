using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FoodLoop.Application.Common.DTOs.AI;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Interfaces.AI;
using FoodLoop.Application.Features.AiIntegration.Commands;
using FoodLoop.Application.Features.AiIntegration.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Features.AiIntegration.Commands;
using FoodLoop.Infrastructure.Features.AiIntegration.Queries;
using FoodLoop.Infrastructure.Integrations.AiService;
using FoodLoop.Infrastructure.Options;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Polly;
using Polly.Registry;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Integrations;

[Trait("Category", "Integration")]
public class E2EIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<FoodLoop.Infrastructure.Persistence.ApplicationDbContext> _dbOptions;
    private readonly ServiceProvider _serviceProvider;
    private readonly IAiServiceClient _realAiClient;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;

    public E2EIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<FoodLoop.Infrastructure.Persistence.ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Build database tables
        using (var setupContext = new E2ETestApplicationDbContext(_dbOptions))
        {
            setupContext.Database.EnsureCreated();
        }

        // 2. Setup Real HttpClient pointing to running Python AI Service
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AiService:BaseUrl", "http://localhost:8000" },
                { "AiService:TimeoutSeconds", "15" },
                { "MonitoringScanner:IntervalMinutes", "60" },
                { "MonitoringScanner:ExpirationThresholdDays", "3" },
                { "MonitoringScanner:VelocityThresholdMultiplier", "0.8" },
                { "AiPricingBatch:IntervalMinutes", "60" },
                { "HistoricalIngestion:IntervalMinutes", "60" },
                { "HistoricalIngestion:BatchSize", "100" }
            })
            .Build();

        services.AddSingleton(typeof(ILogger<>), typeof(InMemoryLogger<>));
        services.AddLogging(builder => {
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        services.AddSingleton<IConfiguration>(configuration);

        // Core Correlation ID Setup
        var mockCorrelation = new Mock<ICorrelationIdAccessor>();
        mockCorrelation.Setup(x => x.GetCorrelationId()).Returns("e2e-verification-correlation-id");
        _correlationIdAccessor = mockCorrelation.Object;
        services.AddSingleton(_correlationIdAccessor);
        services.AddTransient<CorrelationIdDelegatingHandler>();

        // Register custom resilience pipeline provider (Simple retry/timeout for tests)
        services.AddResiliencePipeline<string, HttpResponseMessage>("AiServiceBusinessPipeline", builder =>
        {
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
                Timeout = TimeSpan.FromSeconds(10)
            });
        });

        services.AddResiliencePipeline<string, HttpResponseMessage>("AiServiceHealthPipeline", builder =>
        {
            builder.AddTimeout(new Polly.Timeout.TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(5)
            });
        });

        // Register Options
        services.AddOptions<AiServiceOptions>()
            .Bind(configuration.GetSection(AiServiceOptions.SectionName));
        services.AddOptions<MonitoringScannerOptions>()
            .Bind(configuration.GetSection(MonitoringScannerOptions.SectionName));
        services.AddOptions<PricingBatchOptions>()
            .Bind(configuration.GetSection(PricingBatchOptions.SectionName));
        services.AddOptions<HistoricalIngestionOptions>()
            .Bind(configuration.GetSection(HistoricalIngestionOptions.SectionName));

        // Register DbContext factory pointing to SQLite in-memory DB
        services.AddScoped<IApplicationDbContext>(sp => new E2ETestApplicationDbContext(_dbOptions));

        // Register Real HTTP Client and Client implementation
        services.AddHttpClient<IAiServiceClient, AiServiceClient>((sp, client) =>
        {
            client.BaseAddress = new Uri("http://localhost:8000");
        })
        .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

        // Register MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(RunMonitoringScanCommandHandler).Assembly));

        services.AddSingleton<TimeProvider>(TimeProvider.System);

        _serviceProvider = services.BuildServiceProvider();
        _realAiClient = _serviceProvider.GetRequiredService<IAiServiceClient>();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }

    private E2ETestApplicationDbContext CreateDbContext()
    {
        return new E2ETestApplicationDbContext(_dbOptions);
    }

    [Fact]
    public async Task Liveness_and_Readiness_should_confirm_healthy_python_service()
    {
        var health = await _realAiClient.GetHealthAsync();
        health.Status.Should().Be("ok");

        var ready = await _realAiClient.GetReadyAsync();
        ready.Status.Should().Be("ready");
    }

    [Fact]
    public async Task Direct_Monitoring_Call_should_succeed()
    {
        var org = new Organization { Id = Guid.NewGuid(), Name = "Test Store", AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Produce" };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Apples",
            OriginalPrice = 100m,
            DiscountedPrice = 100m,
            QuantityAvailable = 10,
            ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Organization = org,
            Category = category
        };

        var requestDto = new MonitoringRequestDto(
            Product: new MonitoringProductDto(product.Id.ToString(), product.Title, category.Name),
            Inventory: new MonitoringInventoryDto(product.QuantityAvailable, product.OriginalPrice, product.DiscountedPrice, 80m),
            Demand: new MonitoringDemandDto(1.5, new MonitoringHistoricalSalesDto(2.0)),
            Expiry: new MonitoringExpiryDto(DateTimeOffset.UtcNow.AddDays(1), 24.0),
            Location: new MonitoringLocationDto(30.0, 31.0, org.Id.ToString()),
            StorePolicy: new MonitoringStorePolicyDto(org.Id.ToString(), "assisted"),
            Timestamp: DateTimeOffset.UtcNow
        );

        var response = await _realAiClient.AnalyzeMonitoringAsync(requestDto);
        response.Should().NotBeNull();
        response.Route.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task E2E_Assisted_Workflow_should_route_and_wait_for_approval()
    {
        InMemoryLogStore.Logs.Clear();
        try
        {
            using var dbContext = CreateDbContext();
            var mediator = _serviceProvider.GetRequiredService<IMediator>();

            var merchantUserId = Guid.NewGuid();
            // 1. Seed Store, Category, Product (Near expiry, low sales, Assisted Mode)
            var org = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Assisted Mode E2E Store",
                OwnerId = merchantUserId,
                AiOperatingMode = AiOperatingMode.Assisted,
                Latitude = 30.0713,
                Longitude = 31.2826
            };
            var category = new Category { Id = Guid.NewGuid(), Name = "Bakery" };
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Title = "E2E Chocolate Cake",
                OriginalPrice = 120.00m,
                DiscountedPrice = 120.00m,
                QuantityAvailable = 5,
                ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), // Expiring tomorrow
                Status = ProductStatus.Active,
                OrganizationId = org.Id,
                Organization = org,
                CategoryId = category.Id,
                Category = category,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.Users.Add(new ApplicationUser { Id = merchantUserId, UserName = "e2e_merchant@test.com", Email = "e2e_merchant@test.com", SecurityStamp = Guid.NewGuid().ToString() });
            dbContext.Organizations.Add(org);
            dbContext.Categories.Add(category);
            dbContext.Products.Add(product);
            await dbContext.SaveChangesAsync();

            // 2. Trigger Monitoring Scan (Routes to PRICING)
            var scanResult = await mediator.Send(new RunMonitoringScanCommand());
            scanResult.Success.Should().BeTrue();

            // Assert AiRiskAssessment created with IsPricingStaged = true
            var assessment = await dbContext.AiRiskAssessments.FirstOrDefaultAsync(a => a.ProductId == product.Id);
            assessment.Should().NotBeNull();
            assessment!.Route.Should().Be(AiRoute.PRICING);
            assessment.IsPricingStaged.Should().BeTrue();

            // 3. Trigger Pricing Recommendation Batch
            var pricingResult = await mediator.Send(new RunPricingBatchCommand());
            pricingResult.Success.Should().BeTrue();

            // Assert AiPricingRecommendation is saved as Pending (without mutating product price)
            var recommendation = await dbContext.AiPricingRecommendations.FirstOrDefaultAsync(r => r.ProductId == product.Id);
            recommendation.Should().NotBeNull();
            recommendation!.Status.Should().Be(AiRecommendationStatus.Pending);
            recommendation.ActionRequirement.Should().Be(AiActionRequirement.APPROVAL_REQUIRED);

            // Verify product price is STILL original
            var productAfterPricing = await dbContext.Products.FindAsync(product.Id);
            productAfterPricing!.DiscountedPrice.Should().Be(120.00m);

            // 4. Approve the recommendation
            var approveResult = await mediator.Send(new ApproveAiRecommendationCommand(merchantUserId, recommendation.Id));
            approveResult.Success.Should().BeTrue(approveResult.Message);

            // Verify product price IS updated and recommendation status is Executed
            dbContext.ChangeTracker.Clear();
            var approvedProduct = await dbContext.Products.FindAsync(product.Id);
            var expectedNewPrice = Math.Round(product.OriginalPrice * (1 - (recommendation.DiscountPercentage / 100m)), 2);
            approvedProduct!.DiscountedPrice.Should().Be(expectedNewPrice);

            var approvedRecommendation = await dbContext.AiPricingRecommendations.FindAsync(recommendation.Id);
            approvedRecommendation!.Status.Should().Be(AiRecommendationStatus.Approved);

            // Verify PriceHistory row was written
            var priceHistory = await dbContext.PriceHistories.FirstOrDefaultAsync(ph => ph.ProductId == product.Id);
            priceHistory.Should().NotBeNull();
            priceHistory!.NewDiscountedPrice.Should().Be(expectedNewPrice);
        }
        catch (Exception ex)
        {
            throw new Exception($"Test failed. Logs:\n{string.Join("\n", InMemoryLogStore.Logs)}", ex);
        }
    }

    [Fact]
    public async Task E2E_Autonomous_Workflow_should_auto_execute_when_passing_floor()
    {
        using var dbContext = CreateDbContext();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        // 1. Seed Store, Category, Product (Autonomous Mode)
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Autonomous Mode E2E Store",
            AiOperatingMode = AiOperatingMode.Autonomous,
            Latitude = 30.0713,
            Longitude = 31.2826
        };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        
        // System wide price floor policy configures 20% max discount, so price floor is 100 * 0.80 = 80.
        // AI recommends up to 15% discount, so price is 100 * 0.85 = 85 (above floor, passes).
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "E2E Fresh Milk",
            OriginalPrice = 100.00m,
            DiscountedPrice = 100.00m,
            QuantityAvailable = 10,
            ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Status = ProductStatus.Active,
            OrganizationId = org.Id,
            Organization = org,
            CategoryId = category.Id,
            Category = category,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        // 2. Trigger Monitoring Scan
        var scanResult = await mediator.Send(new RunMonitoringScanCommand());
        scanResult.Success.Should().BeTrue();

        // 3. Trigger Pricing Recommendation Batch
        var pricingResult = await mediator.Send(new RunPricingBatchCommand());
        pricingResult.Success.Should().BeTrue();

        dbContext.ChangeTracker.Clear();
        // Assert recommendation is AutoExecuted, product price IS mutated, PriceHistory written
        var recommendation = await dbContext.AiPricingRecommendations.FirstOrDefaultAsync(r => r.ProductId == product.Id);
        recommendation.Should().NotBeNull();
        recommendation!.Status.Should().Be(AiRecommendationStatus.AutoExecuted);
        recommendation.ActionRequirement.Should().Be(AiActionRequirement.AUTOMATIC_EXECUTION_ELIGIBLE);

        var updatedProduct = await dbContext.Products.FindAsync(product.Id);
        var expectedNewPrice = Math.Round(product.OriginalPrice * (1 - (recommendation.DiscountPercentage / 100m)), 2);
        updatedProduct!.DiscountedPrice.Should().Be(expectedNewPrice);

        var priceHistory = await dbContext.PriceHistories.FirstOrDefaultAsync(ph => ph.ProductId == product.Id);
        priceHistory.Should().NotBeNull();
        priceHistory!.NewDiscountedPrice.Should().Be(expectedNewPrice);
    }

    [Fact]
    public async Task E2E_Autonomous_Workflow_should_reject_when_failing_floor()
    {
        using var dbContext = CreateDbContext();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        // 1. Seed Store, Category, Product (Autonomous Mode)
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Autonomous Mode E2E Store Fail Floor",
            AiOperatingMode = AiOperatingMode.Autonomous,
            Latitude = 30.0713,
            Longitude = 31.2826
        };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        
        // Engineer a failure: set product's live DiscountedPrice low already (e.g. 50% discount).
        // If AI recommends any further discount (even 1%), it will result in NewPrice < Floor.
        // Max system discount allows 20% discount on OriginalPrice (floor = 80).
        // Since product starts with DiscountedPrice = 70m (which is already below floor), any new recommendation fails.
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "E2E Failing Milk",
            OriginalPrice = 100.00m,
            DiscountedPrice = 70.00m, // Pre-discounted past floor
            QuantityAvailable = 10,
            ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Status = ProductStatus.Active,
            OrganizationId = org.Id,
            Organization = org,
            CategoryId = category.Id,
            Category = category,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        // 2. Trigger Monitoring Scan
        var scanResult = await mediator.Send(new RunMonitoringScanCommand());
        scanResult.Success.Should().BeTrue();

        // 3. Trigger Pricing Recommendation Batch
        var pricingResult = await mediator.Send(new RunPricingBatchCommand());
        pricingResult.Success.Should().BeTrue();

        dbContext.ChangeTracker.Clear();
        // Assert recommendation is Rejected (due to floor violation) and price is NOT mutated
        var recommendation = await dbContext.AiPricingRecommendations.FirstOrDefaultAsync(r => r.ProductId == product.Id);
        recommendation.Should().NotBeNull();
        recommendation!.Status.Should().Be(AiRecommendationStatus.Rejected);

        var finalProduct = await dbContext.Products.FindAsync(product.Id);
        finalProduct!.DiscountedPrice.Should().Be(70.00m); // Kept original pre-discounted price

        var priceHistory = await dbContext.PriceHistories.FirstOrDefaultAsync(ph => ph.ProductId == product.Id);
        priceHistory.Should().BeNull(); // No new price history written
    }

    [Fact]
    public async Task E2E_Historical_Ingestion_should_succeed()
    {
        using var dbContext = CreateDbContext();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        // 1. Seed product that is expired or deleted, with a discount event to sweep
        var org = new Organization { Id = Guid.NewGuid(), Name = "Ingestion Store" };
        var category = new Category { Id = Guid.NewGuid(), Name = "Produce" };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "E2E Bananas",
            OriginalPrice = 10.00m,
            DiscountedPrice = 9.00m,
            QuantityAvailable = 0, // Candidate
            ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), // Expired
            Status = ProductStatus.Active,
            OrganizationId = org.Id,
            Organization = org,
            CategoryId = category.Id,
            Category = category,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var discountEvent = new PriceHistory
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            OldOriginalPrice = 10.00m,
            NewOriginalPrice = 10.00m,
            OldDiscountedPrice = 10.00m,
            NewDiscountedPrice = 9.00m, // actual discount event (10% discount)
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-5)
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.PriceHistories.Add(discountEvent);
        await dbContext.SaveChangesAsync();

        // 2. Trigger Ingestion Command
        var result = await mediator.Send(new RunHistoricalIngestionCommand());
        result.Success.Should().BeTrue();

        // Verify ProductPricingEpisode is created in DB
        var episode = await dbContext.ProductPricingEpisodes.FirstOrDefaultAsync(e => e.ProductId == product.Id);
        episode.Should().NotBeNull();
        episode!.EventId.Should().Be($"ep-{product.Id}-{discountEvent.Id}");
        episode.Outcome.Should().Be("SOLD_OUT");
        episode.Outcome.Should().Be("SOLD_OUT");
    }

    [Fact]
    public async Task Full_flow_should_preserve_single_CorrelationId_from_monitoring_through_PriceHistory()
    {
        var testCorrelationId = "e2e-correlation-flow-999";
        InMemoryLogStore.Logs.Clear();

        try
        {
            using var dbContext = CreateDbContext();
            var mediator = _serviceProvider.GetRequiredService<IMediator>();

            // Setup correlation ID accessor mock behavior in scope if needed, or rely on ambient accessor setting.
            // Let's seed the store, category, and user.
            var merchantUserId = Guid.NewGuid();
            var user = new ApplicationUser { Id = merchantUserId, UserName = "e2e_flow_merchant", Email = "e2e_flow_merchant@test.com", SecurityStamp = Guid.NewGuid().ToString() };
            var org = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "E2E Correlation Flow Store",
                OwnerId = merchantUserId,
                AiOperatingMode = AiOperatingMode.Assisted,
                Latitude = 30.0713,
                Longitude = 31.2826
            };
            var category = new Category { Id = Guid.NewGuid(), Name = "Fresh Fruits" };
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Title = "E2E Correlation Flow Banana",
                OriginalPrice = 50.00m,
                DiscountedPrice = 50.00m,
                QuantityAvailable = 10,
                ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                Status = ProductStatus.Active,
                OrganizationId = org.Id,
                Organization = org,
                CategoryId = category.Id,
                Category = category,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.Users.Add(user);
            dbContext.Organizations.Add(org);
            dbContext.Categories.Add(category);
            dbContext.Products.Add(product);
            await dbContext.SaveChangesAsync();

            // Set the correlation ID accessor ambient value for this test execution
            Mock.Get(_correlationIdAccessor).Setup(a => a.GetCorrelationId()).Returns(testCorrelationId);

            // 1. Trigger RunMonitoringScanCommand
            var scanResult = await mediator.Send(new RunMonitoringScanCommand());
            scanResult.Success.Should().BeTrue();

            dbContext.ChangeTracker.Clear();
            var assessment = await dbContext.AiRiskAssessments.FirstOrDefaultAsync(a => a.ProductId == product.Id);
            assessment.Should().NotBeNull();
            assessment!.CorrelationId.Should().Be(testCorrelationId);
            assessment.Route.Should().Be(AiRoute.PRICING);

            // 2. Trigger RunPricingBatchCommand
            var pricingResult = await mediator.Send(new RunPricingBatchCommand());
            pricingResult.Success.Should().BeTrue();

            dbContext.ChangeTracker.Clear();
            var recommendation = await dbContext.AiPricingRecommendations.FirstOrDefaultAsync(r => r.ProductId == product.Id);
            recommendation.Should().NotBeNull();
            recommendation!.CorrelationId.Should().Be(testCorrelationId);
            recommendation.Status.Should().Be(AiRecommendationStatus.Pending);

            // 3. Trigger ApproveAiRecommendationCommand
            var approveResult = await mediator.Send(new ApproveAiRecommendationCommand(merchantUserId, recommendation.Id));
            approveResult.Success.Should().BeTrue(approveResult.Message);

            dbContext.ChangeTracker.Clear();
            var priceHistory = await dbContext.PriceHistories.FirstOrDefaultAsync(ph => ph.ProductId == product.Id);
            priceHistory.Should().NotBeNull();
            priceHistory!.ChangeReason.Should().Contain(testCorrelationId);
        }
        catch (Exception ex)
        {
            var logs = string.Join("\n", InMemoryLogStore.Logs);
            throw new Exception($"Test failed. Logs:\n{logs}", ex);
        }
    }
}

public static class InMemoryLogStore
{
    public static readonly List<string> Logs = new();
}

public class InMemoryLogger<T> : ILogger<T>, IDisposable
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => this;
    public void Dispose() {}

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var msg = formatter(state, exception);
        if (exception != null)
        {
            msg += "\n" + exception.ToString();
        }
        lock (InMemoryLogStore.Logs)
        {
            InMemoryLogStore.Logs.Add($"[{logLevel}] {msg}");
        }
    }
}

public class E2ETestApplicationDbContext : FoodLoop.Infrastructure.Persistence.ApplicationDbContext
{
    public E2ETestApplicationDbContext(DbContextOptions<FoodLoop.Infrastructure.Persistence.ApplicationDbContext> options)
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
                property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.DateTimeOffsetToBinaryConverter());
            }
        }
    }
}
