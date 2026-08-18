using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FoodLoop.Application.Common.DTOs.AI;
using FoodLoop.Application.Common.Exceptions.AI;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Interfaces.AI;
using FoodLoop.Application.Features.AiIntegration.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.AiIntegration.Commands;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Options;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.AiIntegration;

public class AiIntegrationEdgeCaseTests
{
    private readonly Mock<IAiServiceClient> _mockAiClient;
    private readonly Mock<ICorrelationIdAccessor> _mockCorrelationAccessor;
    private readonly Mock<ILogger<RunPricingBatchCommandHandler>> _mockBatchLogger;
    private readonly Mock<ILogger<ApproveAiRecommendationCommandHandler>> _mockApproveLogger;
    private readonly Mock<ILogger<RunMonitoringScanCommandHandler>> _mockScannerLogger;
    private readonly TimeProvider _timeProvider;

    public AiIntegrationEdgeCaseTests()
    {
        _mockAiClient = new Mock<IAiServiceClient>();
        _mockCorrelationAccessor = new Mock<ICorrelationIdAccessor>();
        _mockBatchLogger = new Mock<ILogger<RunPricingBatchCommandHandler>>();
        _mockApproveLogger = new Mock<ILogger<ApproveAiRecommendationCommandHandler>>();
        _mockScannerLogger = new Mock<ILogger<RunMonitoringScanCommandHandler>>();
        _timeProvider = TimeProvider.System;

        _mockCorrelationAccessor.Setup(x => x.GetCorrelationId()).Returns("edge-correlation-id");
    }

    private FoodLoop.Infrastructure.Persistence.ApplicationDbContext CreateSqliteContext()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<FoodLoop.Infrastructure.Persistence.ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new TestApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task Scanner_Handle_should_persist_zero_risk_assessments_on_failure()
    {
        // Arrange: 2 products, AI scan for product A throws exception, product B succeeds.
        using var dbContext = CreateSqliteContext();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Scanner Failure Store", AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Produce" };
        var prodA = new Product { Id = Guid.NewGuid(), Title = "Product A (Fails)", ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), OriginalPrice = 10m, DiscountedPrice = 10m, Organization = org, Category = category, Status = ProductStatus.Active };
        var prodB = new Product { Id = Guid.NewGuid(), Title = "Product B (Succeeds)", ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), OriginalPrice = 20m, DiscountedPrice = 20m, Organization = org, Category = category, Status = ProductStatus.Active };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.AddRange(prodA, prodB);
        await dbContext.SaveChangesAsync();

        _mockAiClient.Setup(x => x.AnalyzeMonitoringAsync(It.Is<MonitoringRequestDto>(r => r.Product.Id == prodA.Id.ToString()), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Mock AI Monitoring Network Failure"));

        _mockAiClient.Setup(x => x.AnalyzeMonitoringAsync(It.Is<MonitoringRequestDto>(r => r.Product.Id == prodB.Id.ToString()), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MonitoringResponseDto("PRICING", "CRITICAL", "Near expiry", 0.95));

        var mockScannerOptions = new Mock<IOptions<MonitoringScannerOptions>>();
        mockScannerOptions.Setup(x => x.Value).Returns(new MonitoringScannerOptions
        {
            ExpirationThresholdDays = 3,
            VelocityThresholdMultiplier = 0.8
        });

        var handler = new RunMonitoringScanCommandHandler(
            dbContext,
            _mockAiClient.Object,
            _mockCorrelationAccessor.Object,
            mockScannerOptions.Object,
            _timeProvider,
            _mockScannerLogger.Object
        );

        // Act
        var result = await handler.Handle(new RunMonitoringScanCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue(); // Scanner runs gracefully past single failures
        
        var assessments = await dbContext.AiRiskAssessments.ToListAsync();
        assessments.Any(a => a.ProductId == prodA.Id).Should().BeFalse(); // Zero rows for failed candidate (No-fabrication)
        assessments.Any(a => a.ProductId == prodB.Id).Should().BeTrue(); // Successfully persisted row for sibling
    }

    [Fact]
    public async Task Batch_Handle_should_persist_zero_recommendations_for_failed_store()
    {
        // Arrange: Store A and Store B staged. Store A batch call fails; Store B succeeds.
        using var dbContext = CreateSqliteContext();

        var orgA = new Organization { Id = Guid.NewGuid(), Name = "Failed Store", AiOperatingMode = AiOperatingMode.Assisted };
        var orgB = new Organization { Id = Guid.NewGuid(), Name = "Succeeds Store", AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var prodA = new Product { Id = Guid.NewGuid(), Title = "Failed Product", OriginalPrice = 10m, DiscountedPrice = 10m, Organization = orgA, Category = category };
        var prodB = new Product { Id = Guid.NewGuid(), Title = "Succeeds Product", OriginalPrice = 20m, DiscountedPrice = 20m, Organization = orgB, Category = category };

        var riskA = new AiRiskAssessment(prodA.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Risk A", 0.9, "corr", isPricingStaged: true);
        var riskB = new AiRiskAssessment(prodB.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Risk B", 0.9, "corr", isPricingStaged: true);

        dbContext.Organizations.AddRange(orgA, orgB);
        dbContext.Categories.Add(category);
        dbContext.Products.AddRange(prodA, prodB);
        dbContext.AiRiskAssessments.AddRange(riskA, riskB);
        await dbContext.SaveChangesAsync();

        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.Is<PricingBatchRequestDto>(r => r.StoreId == orgA.Id.ToString()), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Store pricing recommendations service error"));

        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.Is<PricingBatchRequestDto>(r => r.StoreId == orgB.Id.ToString()), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingBatchResponseDto(orgB.Id.ToString(), new List<PricingDecisionDto>
            {
                new PricingDecisionDto(prodB.Id.ToString(), 10.0, "Reason", 0.9, "APPROVAL_REQUIRED", "Reason")
            }));

        var handler = new RunPricingBatchCommandHandler(
            dbContext,
            _mockAiClient.Object,
            _mockCorrelationAccessor.Object,
            _timeProvider,
            _mockBatchLogger.Object
        );

        // Act
        var result = await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        var recs = await dbContext.AiPricingRecommendations.ToListAsync();
        recs.Any(r => r.OrganizationId == orgA.Id).Should().BeFalse(); // Zero rows for failed store (No-fabrication)
        recs.Any(r => r.OrganizationId == orgB.Id).Should().BeTrue(); // Intact rows for succeeding store
    }

    [Fact]
    public async Task Approve_recommendation_should_be_rejected_if_product_quantity_changed_since_staging()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var merchantUserId = Guid.NewGuid();
        var user = new ApplicationUser { Id = merchantUserId, UserName = "merchant", Email = "merchant@test.com", SecurityStamp = Guid.NewGuid().ToString() };
        var org = new Organization { Id = Guid.NewGuid(), Name = "Assisted Store", OwnerId = merchantUserId, AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Bakery" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Snack", OriginalPrice = 100m, DiscountedPrice = 100m, QuantityAvailable = 10, Status = ProductStatus.Active, Organization = org, Category = category };

        dbContext.Users.Add(user);
        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var recommendation = new AiPricingRecommendation(
            product.Id, org.Id, 10.0m, "Reason", 0.9, AiActionRequirement.APPROVAL_REQUIRED, "Reason", "corr-id", AiRecommendationStatus.Pending
        )
        {
            SnapshotOriginalPrice = 100m,
            SnapshotQuantityAvailable = 10, // Originally 10
            SnapshotProductStatus = ProductStatus.Active
        };
        dbContext.AiPricingRecommendations.Add(recommendation);
        await dbContext.SaveChangesAsync();

        // Simulate quantity change after staging
        product.QuantityAvailable = 5;
        await dbContext.SaveChangesAsync();

        var handler = new ApproveAiRecommendationCommandHandler(dbContext, _timeProvider, _mockApproveLogger.Object);

        // Act
        var result = await handler.Handle(new ApproveAiRecommendationCommand(merchantUserId, recommendation.Id), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Stale Recommendation - Product State Changed");

        dbContext.ChangeTracker.Clear();
        var rec = await dbContext.AiPricingRecommendations.FindAsync(recommendation.Id);
        rec!.Status.Should().Be(AiRecommendationStatus.Rejected);
        rec.ActionReason.Should().Be("Stale Recommendation - Product State Changed");

        var productAfter = await dbContext.Products.FindAsync(product.Id);
        productAfter!.DiscountedPrice.Should().Be(100m); // Unchanged
    }

    [Fact]
    public async Task Approve_recommendation_should_be_rejected_if_product_status_changed_since_staging()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var merchantUserId = Guid.NewGuid();
        var user = new ApplicationUser { Id = merchantUserId, UserName = "merchant", Email = "merchant@test.com", SecurityStamp = Guid.NewGuid().ToString() };
        var org = new Organization { Id = Guid.NewGuid(), Name = "Assisted Store", OwnerId = merchantUserId, AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Bakery" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Snack", OriginalPrice = 100m, DiscountedPrice = 100m, QuantityAvailable = 10, Status = ProductStatus.Active, Organization = org, Category = category };

        dbContext.Users.Add(user);
        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var recommendation = new AiPricingRecommendation(
            product.Id, org.Id, 10.0m, "Reason", 0.9, AiActionRequirement.APPROVAL_REQUIRED, "Reason", "corr-id", AiRecommendationStatus.Pending
        )
        {
            SnapshotOriginalPrice = 100m,
            SnapshotQuantityAvailable = 10,
            SnapshotProductStatus = ProductStatus.Active
        };
        dbContext.AiPricingRecommendations.Add(recommendation);
        await dbContext.SaveChangesAsync();

        // Simulate status change to Hidden
        product.Status = ProductStatus.Hidden;
        await dbContext.SaveChangesAsync();

        var handler = new ApproveAiRecommendationCommandHandler(dbContext, _timeProvider, _mockApproveLogger.Object);

        // Act
        var result = await handler.Handle(new ApproveAiRecommendationCommand(merchantUserId, recommendation.Id), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Stale Recommendation - Product State Changed");

        dbContext.ChangeTracker.Clear();
        var rec = await dbContext.AiPricingRecommendations.FindAsync(recommendation.Id);
        rec!.Status.Should().Be(AiRecommendationStatus.Rejected);

        var productAfter = await dbContext.Products.FindAsync(product.Id);
        productAfter!.DiscountedPrice.Should().Be(100m); // Unchanged
    }

    [Fact]
    public async Task Autonomous_execution_should_be_rejected_if_product_state_changed_since_staging()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Auto Store", AiOperatingMode = AiOperatingMode.Autonomous };
        var category = new Category { Id = Guid.NewGuid(), Name = "Produce" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Apples", OriginalPrice = 100m, DiscountedPrice = 100m, QuantityAvailable = 50, Status = ProductStatus.Active, Organization = org, Category = category };

        // Captured with quantity 50
        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Staged", 0.9, "corr", isPricingStaged: true)
        {
            SnapshotOriginalPrice = 100m,
            SnapshotQuantityAvailable = 50,
            SnapshotProductStatus = ProductStatus.Active
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.AiRiskAssessments.Add(risk);
        await dbContext.SaveChangesAsync();

        // Simulate quantity modification AFTER staging but BEFORE recommendations processing runs
        product.QuantityAvailable = 30;
        await dbContext.SaveChangesAsync();

        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.IsAny<PricingBatchRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingBatchResponseDto(org.Id.ToString(), new List<PricingDecisionDto>
            {
                new PricingDecisionDto(product.Id.ToString(), 10.0, "Decide Apples", 0.95, "AUTOMATIC_EXECUTION_ELIGIBLE", "Reason")
            }));

        var handler = new RunPricingBatchCommandHandler(
            dbContext,
            _mockAiClient.Object,
            _mockCorrelationAccessor.Object,
            _timeProvider,
            _mockBatchLogger.Object
        );

        // Act
        var result = await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        dbContext.ChangeTracker.Clear();
        var rec = await dbContext.AiPricingRecommendations.FirstOrDefaultAsync(r => r.ProductId == product.Id);
        rec.Should().NotBeNull();
        rec!.Status.Should().Be(AiRecommendationStatus.Rejected);
        rec.ActionReason.Should().Be("Stale Recommendation - Product State Changed");

        var productAfter = await dbContext.Products.FindAsync(product.Id);
        productAfter!.DiscountedPrice.Should().Be(100m); // Pricing preserved
    }

    [Fact]
    public async Task Approve_recommendation_should_succeed_if_product_state_unchanged()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var merchantUserId = Guid.NewGuid();
        var user = new ApplicationUser { Id = merchantUserId, UserName = "merchant", Email = "merchant@test.com", SecurityStamp = Guid.NewGuid().ToString() };
        var org = new Organization { Id = Guid.NewGuid(), Name = "Assisted Store", OwnerId = merchantUserId, AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Bakery" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Snack", OriginalPrice = 100m, DiscountedPrice = 100m, QuantityAvailable = 10, Status = ProductStatus.Active, Organization = org, Category = category };

        dbContext.Users.Add(user);
        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var recommendation = new AiPricingRecommendation(
            product.Id, org.Id, 10.0m, "Reason", 0.9, AiActionRequirement.APPROVAL_REQUIRED, "Reason", "corr-id", AiRecommendationStatus.Pending
        )
        {
            SnapshotOriginalPrice = 100m,
            SnapshotQuantityAvailable = 10,
            SnapshotProductStatus = ProductStatus.Active
        };
        dbContext.AiPricingRecommendations.Add(recommendation);
        await dbContext.SaveChangesAsync();

        var handler = new ApproveAiRecommendationCommandHandler(dbContext, _timeProvider, _mockApproveLogger.Object);

        // Act
        var result = await handler.Handle(new ApproveAiRecommendationCommand(merchantUserId, recommendation.Id), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue(result.Message);

        dbContext.ChangeTracker.Clear();
        var rec = await dbContext.AiPricingRecommendations.FindAsync(recommendation.Id);
        rec!.Status.Should().Be(AiRecommendationStatus.Approved);

        var productAfter = await dbContext.Products.FindAsync(product.Id);
        productAfter!.DiscountedPrice.Should().Be(90m); // Successful price change
    }

    [Fact]
    public async Task Approve_recommendation_should_be_rejected_if_product_is_no_longer_Active()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var merchantUserId = Guid.NewGuid();
        var user = new ApplicationUser { Id = merchantUserId, UserName = "merchant", Email = "merchant@test.com", SecurityStamp = Guid.NewGuid().ToString() };
        var org = new Organization { Id = Guid.NewGuid(), Name = "Assisted Store", OwnerId = merchantUserId, AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Bakery" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Snack", OriginalPrice = 100m, DiscountedPrice = 100m, QuantityAvailable = 10, Status = ProductStatus.Active, Organization = org, Category = category };

        dbContext.Users.Add(user);
        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var recommendation = new AiPricingRecommendation(
            product.Id, org.Id, 10.0m, "Reason", 0.9, AiActionRequirement.APPROVAL_REQUIRED, "Reason", "corr-id", AiRecommendationStatus.Pending
        )
        {
            SnapshotOriginalPrice = 100m,
            SnapshotQuantityAvailable = 10,
            SnapshotProductStatus = ProductStatus.Active
        };
        dbContext.AiPricingRecommendations.Add(recommendation);
        await dbContext.SaveChangesAsync();

        // Simulate transition to SoldOut status (no longer Active)
        product.Status = ProductStatus.SoldOut;
        await dbContext.SaveChangesAsync();

        var handler = new ApproveAiRecommendationCommandHandler(dbContext, _timeProvider, _mockApproveLogger.Object);

        // Act
        var result = await handler.Handle(new ApproveAiRecommendationCommand(merchantUserId, recommendation.Id), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Stale Recommendation - Product State Changed");

        dbContext.ChangeTracker.Clear();
        var rec = await dbContext.AiPricingRecommendations.FindAsync(recommendation.Id);
        rec!.Status.Should().Be(AiRecommendationStatus.Rejected);
    }

    [Fact]
    public async Task Autonomous_execution_should_be_rejected_if_product_is_no_longer_Active()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Auto Store", AiOperatingMode = AiOperatingMode.Autonomous };
        var category = new Category { Id = Guid.NewGuid(), Name = "Produce" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Apples", OriginalPrice = 100m, DiscountedPrice = 100m, QuantityAvailable = 50, Status = ProductStatus.Active, Organization = org, Category = category };

        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Staged", 0.9, "corr", isPricingStaged: true)
        {
            SnapshotOriginalPrice = 100m,
            SnapshotQuantityAvailable = 50,
            SnapshotProductStatus = ProductStatus.Active
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.AiRiskAssessments.Add(risk);
        await dbContext.SaveChangesAsync();

        // Transition product status to Hidden (no longer Active)
        product.Status = ProductStatus.Hidden;
        await dbContext.SaveChangesAsync();

        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.IsAny<PricingBatchRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingBatchResponseDto(org.Id.ToString(), new List<PricingDecisionDto>
            {
                new PricingDecisionDto(product.Id.ToString(), 10.0, "Decide Apples", 0.95, "AUTOMATIC_EXECUTION_ELIGIBLE", "Reason")
            }));

        var handler = new RunPricingBatchCommandHandler(
            dbContext,
            _mockAiClient.Object,
            _mockCorrelationAccessor.Object,
            _timeProvider,
            _mockBatchLogger.Object
        );

        // Act
        var result = await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        dbContext.ChangeTracker.Clear();
        var rec = await dbContext.AiPricingRecommendations.FirstOrDefaultAsync(r => r.ProductId == product.Id);
        rec.Should().NotBeNull();
        rec!.Status.Should().Be(AiRecommendationStatus.Rejected);
        rec.ActionReason.Should().Be("Stale Recommendation - Product State Changed");
    }

    [Fact]
    public async Task Freshness_check_should_reject_or_require_backfill_when_snapshot_was_never_captured()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var merchantUserId = Guid.NewGuid();
        var user = new ApplicationUser { Id = merchantUserId, UserName = "merchant@test.com", Email = "merchant@test.com" };
        dbContext.Users.Add(user);

        var org = new Organization { Id = Guid.NewGuid(), Name = "Assisted Store", AiOperatingMode = AiOperatingMode.Assisted, OwnerId = merchantUserId };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Milk", OriginalPrice = 100m, DiscountedPrice = 100m, Organization = org, Category = category };
        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.9, "corr-id", isPricingStaged: true);

        // Snapshot properties are null (representing pre-migration / uncaptured)
        var recommendation = new AiPricingRecommendation(
            product.Id, org.Id, 5.0m, "Reason", 0.9,
            AiActionRequirement.APPROVAL_REQUIRED, "ActionReason", "corr-id",
            AiRecommendationStatus.Pending, risk.Id
        )
        {
            SnapshotOriginalPrice = null,
            SnapshotQuantityAvailable = null,
            SnapshotProductStatus = null
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.AiRiskAssessments.Add(risk);
        dbContext.AiPricingRecommendations.Add(recommendation);
        await dbContext.SaveChangesAsync();

        var handler = new ApproveAiRecommendationCommandHandler(dbContext, _timeProvider, _mockApproveLogger.Object);
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await handler.Handle(new ApproveAiRecommendationCommand(merchantUserId, recommendation.Id), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Stale Recommendation - Product State Changed");

        dbContext.ChangeTracker.Clear();
        var rec = await dbContext.AiPricingRecommendations.FindAsync(recommendation.Id);
        rec!.Status.Should().Be(AiRecommendationStatus.Rejected);
        rec.ActionReason.Should().Be("Stale Recommendation - Product State Changed");
    }
}
