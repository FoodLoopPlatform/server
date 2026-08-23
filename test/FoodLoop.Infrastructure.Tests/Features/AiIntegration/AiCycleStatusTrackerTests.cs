using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FoodLoop.Application.Features.AiIntegration.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.AiIntegration.Queries;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.AiIntegration;

public class AiCycleStatusTrackerTests
{
    [Fact]
    public void Constructor_ShouldPreseed_AllThreeCycles()
    {
        // Arrange
        var tracker = new AiCycleStatusTracker();

        // Act
        var overview = tracker.GetAllCyclesStatus();

        // Assert
        overview.MonitoringScanner.Should().NotBeNull();
        overview.MonitoringScanner.CycleName.Should().Be("MonitoringScanner");
        overview.MonitoringScanner.IntervalMinutes.Should().Be(60);
        overview.MonitoringScanner.NextRunExpectedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(58));

        overview.PricingBatch.Should().NotBeNull();
        overview.PricingBatch.CycleName.Should().Be("PricingBatch");

        overview.HistoricalIngestion.Should().NotBeNull();
        overview.HistoricalIngestion.CycleName.Should().Be("HistoricalIngestion");

        overview.NextUpcomingCycleAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordCycleStarted_ShouldSet_IsRunningTrue_AndStatusRunning()
    {
        // Arrange
        var tracker = new AiCycleStatusTracker();

        // Act
        tracker.RecordCycleStarted("PricingBatch");
        var status = tracker.GetCycleStatus("PricingBatch");

        // Assert
        status.IsRunning.Should().BeTrue();
        status.Status.Should().Be("Running");
        status.LastRunStartedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void RecordCycleCompleted_ShouldSet_Success_AndCalculateNextRun()
    {
        // Arrange
        var tracker = new AiCycleStatusTracker();
        tracker.RecordCycleStarted("PricingBatch");

        // Act
        tracker.RecordCycleCompleted("PricingBatch", 30);
        var status = tracker.GetCycleStatus("PricingBatch");

        // Assert
        status.IsRunning.Should().BeFalse();
        status.Status.Should().Be("Success");
        status.LastError.Should().BeNull();
        status.IntervalMinutes.Should().Be(30);
        status.LastRunCompletedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        status.NextRunExpectedAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(30), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void RecordCycleFailed_ShouldCapture_ErrorMessage_AndCalculateNextRun()
    {
        // Arrange
        var tracker = new AiCycleStatusTracker();
        tracker.RecordCycleStarted("MonitoringScanner");

        // Act
        tracker.RecordCycleFailed("MonitoringScanner", "Network timeout calling AI service", 45);
        var status = tracker.GetCycleStatus("MonitoringScanner");

        // Assert
        status.IsRunning.Should().BeFalse();
        status.Status.Should().Be("Failed");
        status.LastError.Should().Be("Network timeout calling AI service");
        status.IntervalMinutes.Should().Be(45);
        status.NextRunExpectedAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(45), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task GetAiCycleStatusQueryHandler_ShouldReturn_AllCycles()
    {
        // Arrange
        var tracker = new AiCycleStatusTracker();
        tracker.RecordCycleCompleted("MonitoringScanner", 60);
        var handler = new GetAiCycleStatusQueryHandler(tracker);

        // Act
        var result = await handler.Handle(new GetAiCycleStatusQuery(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.MonitoringScanner.Status.Should().Be("Success");
        result.Data.PricingBatch.CycleName.Should().Be("PricingBatch");
    }

    [Fact]
    public async Task GetStoreAiScheduleQueryHandler_ShouldReturn_StoreAiSchedule()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        var merchantUserId = Guid.NewGuid();
        var store = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = merchantUserId,
            Name = "Test Store",
            AiOperatingMode = AiOperatingMode.Assisted
        };
        await db.Organizations.AddAsync(store);
        await db.SaveChangesAsync();

        var tracker = new AiCycleStatusTracker();
        tracker.RecordCycleCompleted("PricingBatch", 60);
        var handler = new GetStoreAiScheduleQueryHandler(db, tracker);

        // Act
        var result = await handler.Handle(new GetStoreAiScheduleQuery(merchantUserId), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AutomationMode.Should().Be("Assisted");
        result.Data.PricingIntervalMinutes.Should().Be(60);
        result.Data.NextPricingBatchAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(58));
    }

    [Fact]
    public async Task GetPendingAiRecommendationsQueryHandler_ShouldReturn_EnrichedPricingNumbers()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        var merchantUserId = Guid.NewGuid();
        var store = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = merchantUserId,
            Name = "Gourmet Cairo Store",
            AiOperatingMode = AiOperatingMode.Assisted
        };
        await db.Organizations.AddAsync(store);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = store.Id,
            Title = "Greek Yoghurt 150g",
            OriginalPrice = 50.00m,
            DiscountedPrice = 45.00m,
            QuantityAvailable = 20,
            ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            Status = ProductStatus.Active
        };
        product.Images.Add(new ProductImage
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            ImageUrl = "https://images.example.com/yoghurt.jpg",
            DisplayOrder = 1
        });
        await db.Products.AddAsync(product);

        var risk = new AiRiskAssessment
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            RiskLevel = AiRiskLevel.HIGH,
            Confidence = 0.95
        };
        await db.AiRiskAssessments.AddAsync(risk);

        var recommendation = new AiPricingRecommendation
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            OrganizationId = store.Id,
            DiscountPercentage = 10.0m,
            Confidence = 0.94,
            Reason = "2 days remaining with high stock",
            ActionRequirement = AiActionRequirement.APPROVAL_REQUIRED,
            Status = AiRecommendationStatus.Pending,
            RiskAssessmentId = risk.Id,
            SnapshotOriginalPrice = 50.00m,
            SnapshotQuantityAvailable = 20
        };
        await db.AiPricingRecommendations.AddAsync(recommendation);
        await db.SaveChangesAsync();

        var handler = new GetPendingAiRecommendationsQueryHandler(db);

        // Act
        var result = await handler.Handle(new GetPendingAiRecommendationsQuery(merchantUserId), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().HaveCount(1);

        var rec = result.Data![0];
        rec.Id.Should().Be(recommendation.Id);
        rec.ProductName.Should().Be("Greek Yoghurt 150g");
        rec.OriginalPrice.Should().Be(50.00m);
        rec.CurrentPrice.Should().Be(45.00m);
        rec.RecommendedPrice.Should().Be(45.00m); // 50 - (50 * 0.10) = 45.00
        rec.DiscountPercentage.Should().Be(10.0m);
        rec.DiscountAmount.Should().Be(5.00m);
        rec.QuantityAvailable.Should().Be(20);
        rec.ProductImageUrl.Should().Be("https://images.example.com/yoghurt.jpg");
        rec.RiskLevel.Should().Be("HIGH");
        rec.DaysRemaining.Should().Be(2);
    }

    [Fact]
    public void GetCycleStatus_ShouldReturn_Unknown_WhenCycleNotTracked()
    {
        // Arrange
        var tracker = new AiCycleStatusTracker();

        // Act
        var status = tracker.GetCycleStatus("NonExistentCycle");

        // Assert
        status.Should().NotBeNull();
        status.CycleName.Should().Be("NonExistentCycle");
        status.Status.Should().Be("Unknown");
        status.IsRunning.Should().BeFalse();
        status.IntervalMinutes.Should().Be(60);
    }

    [Fact]
    public void InitializeCycle_ShouldUpdate_ExistingCycleInterval()
    {
        // Arrange
        var tracker = new AiCycleStatusTracker();

        // Act
        tracker.InitializeCycle("PricingBatch", 15);
        var status = tracker.GetCycleStatus("PricingBatch");

        // Assert
        status.IntervalMinutes.Should().Be(15);
        status.Status.Should().Be("Scheduled");
    }

    [Fact]
    public async Task GetStoreAiScheduleQueryHandler_ShouldFallbackToAssisted_WhenStoreNotFound()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        var tracker = new AiCycleStatusTracker();
        var handler = new GetStoreAiScheduleQueryHandler(db, tracker);

        // Act
        var result = await handler.Handle(new GetStoreAiScheduleQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AutomationMode.Should().Be("Assisted");
    }

    [Fact]
    public async Task GetPendingAiRecommendationsQueryHandler_ShouldReturn_EmptyList_WhenNoPendingRecommendations()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        var merchantUserId = Guid.NewGuid();
        var store = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = merchantUserId,
            Name = "Empty Store",
            AiOperatingMode = AiOperatingMode.Assisted
        };
        await db.Organizations.AddAsync(store);
        await db.SaveChangesAsync();

        var handler = new GetPendingAiRecommendationsQueryHandler(db);

        // Act
        var result = await handler.Handle(new GetPendingAiRecommendationsQuery(merchantUserId), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingAiRecommendationsQueryHandler_ShouldReturn_OnlyLatestRecommendation_PerProduct()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        var merchantUserId = Guid.NewGuid();
        var store = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = merchantUserId,
            Name = "Deduplication Store",
            AiOperatingMode = AiOperatingMode.Assisted
        };
        await db.Organizations.AddAsync(store);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = store.Id,
            Title = "Fresh Milk 1L",
            OriginalPrice = 40.00m,
            DiscountedPrice = 40.00m,
            QuantityAvailable = 15,
            ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Status = ProductStatus.Active
        };
        await db.Products.AddAsync(product);

        // Older recommendation generated earlier (e.g. 2 hours ago)
        var olderRec = new AiPricingRecommendation
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            OrganizationId = store.Id,
            DiscountPercentage = 5.0m,
            Confidence = 0.85,
            Reason = "Older recommendation 5%",
            ActionRequirement = AiActionRequirement.APPROVAL_REQUIRED,
            Status = AiRecommendationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2)
        };

        // Newer recommendation generated now (e.g. latest cycle)
        var newerRec = new AiPricingRecommendation
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            OrganizationId = store.Id,
            DiscountPercentage = 15.0m,
            Confidence = 0.95,
            Reason = "Latest recommendation 15%",
            ActionRequirement = AiActionRequirement.APPROVAL_REQUIRED,
            Status = AiRecommendationStatus.Pending
        };

        await db.AiPricingRecommendations.AddAsync(olderRec);
        await db.SaveChangesAsync();

        await Task.Delay(100);

        await db.AiPricingRecommendations.AddAsync(newerRec);
        await db.SaveChangesAsync();

        var handler = new GetPendingAiRecommendationsQueryHandler(db);

        // Act
        var result = await handler.Handle(new GetPendingAiRecommendationsQuery(merchantUserId), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().HaveCount(1); // Exactly ONE recommendation returned, not two!

        var returnedRec = result.Data![0];
        returnedRec.Id.Should().Be(newerRec.Id); // Must be the newest one
        returnedRec.DiscountPercentage.Should().Be(15.0m);
        returnedRec.Reason.Should().Be("Latest recommendation 15%");
    }
}
