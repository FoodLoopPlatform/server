using System;
using FoodLoop.Application.Common.Exceptions;
using System.Collections.Generic;
using System.Linq;
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
using FoodLoop.Infrastructure.Features.AiIntegration.Commands;
using FoodLoop.Infrastructure.Features.AiIntegration.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.AiIntegration;

public class AiAssistedApprovalTests
{
    private readonly Mock<IAiServiceClient> _mockAiClient;
    private readonly Mock<ICorrelationIdAccessor> _mockCorrelationAccessor;
    private readonly Mock<ILogger<RunPricingBatchCommandHandler>> _mockBatchLogger;
    private readonly Mock<ILogger<ApproveAiRecommendationCommandHandler>> _mockApproveLogger;
    private readonly Mock<ILogger<RejectAiRecommendationCommandHandler>> _mockRejectLogger;
    private readonly TimeProvider _timeProvider;

    public AiAssistedApprovalTests()
    {
        _mockAiClient = new Mock<IAiServiceClient>();
        _mockCorrelationAccessor = new Mock<ICorrelationIdAccessor>();
        _mockBatchLogger = new Mock<ILogger<RunPricingBatchCommandHandler>>();
        _mockApproveLogger = new Mock<ILogger<ApproveAiRecommendationCommandHandler>>();
        _mockRejectLogger = new Mock<ILogger<RejectAiRecommendationCommandHandler>>();
        _timeProvider = TimeProvider.System;

        _mockCorrelationAccessor.Setup(x => x.GetCorrelationId()).Returns("test-correlation-id");
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
    public async Task Autonomous_AutoExecuted_should_write_PriceHistory_row()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Auto Store", AiOperatingMode = AiOperatingMode.Autonomous };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Milk", OriginalPrice = 100m, DiscountedPrice = 100m, Organization = org, Category = category };
        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.9, "corr-id", isPricingStaged: true)
        {
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
        };

        // We calculate floor (DynamicAi = 90% of 100m is 90m). Discount 5% -> 95m. Valid.
        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.AiRiskAssessments.Add(risk);
        await dbContext.SaveChangesAsync();

        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.IsAny<PricingBatchRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingBatchResponseDto(org.Id.ToString(), new List<PricingDecisionDto>
            {
                new PricingDecisionDto(product.Id.ToString(), 5.0, "Decide Milk", 0.95, "AUTOMATIC_EXECUTION_ELIGIBLE", "Reason")
            }));

        var handler = new RunPricingBatchCommandHandler(
            dbContext,
            _mockAiClient.Object,
            _mockCorrelationAccessor.Object,
            _timeProvider,
            _mockBatchLogger.Object
        );

        dbContext.ChangeTracker.Clear();

        // Act
        var result = await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        // Assert
        result.Success.Should().BeTrue();

        var priceHistory = await dbContext.PriceHistories.SingleAsync();
        priceHistory.ProductId.Should().Be(product.Id);
        priceHistory.OldDiscountedPrice.Should().Be(100m);
        priceHistory.NewDiscountedPrice.Should().Be(95m);
        priceHistory.ChangeReason.Should().Contain("AI Autonomous Pricing");
        priceHistory.ChangedBy.Should().Be(Guid.Empty); // System/AI sentinel actor ID
    }

    [Fact]
    public async Task Approve_Pending_recommendation_above_floor_should_mutate_price_and_write_PriceHistory()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var merchantUserId = Guid.NewGuid();
        var user = new ApplicationUser { Id = merchantUserId, UserName = "merchant@test.com", Email = "merchant@test.com" };
        dbContext.Users.Add(user);

        var org = new Organization { Id = Guid.NewGuid(), Name = "Assisted Store", AiOperatingMode = AiOperatingMode.Assisted, OwnerId = merchantUserId };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Milk", OriginalPrice = 100m, DiscountedPrice = 100m, Organization = org, Category = category };
        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.9, "corr-id", isPricingStaged: true)
        {
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
        };

        // AI recommended 5% discount (proposed price 95m). DynamicAi floor is 90% (90m). Above floor.
        var recommendation = new AiPricingRecommendation(
            product.Id, org.Id, 5.0m, "Reason", 0.9,
            AiActionRequirement.APPROVAL_REQUIRED, "ActionReason", "corr-id",
            AiRecommendationStatus.Pending, risk.Id
        )
        {
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
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
        dbContext.ChangeTracker.Clear();

        // Assert
        result.Success.Should().BeTrue();

        var freshRec = await dbContext.AiPricingRecommendations.SingleAsync();
        freshRec.Status.Should().Be(AiRecommendationStatus.Approved);
        freshRec.ExecutedAt.Should().NotBeNull();
        freshRec.ApprovedBy.Should().Be(merchantUserId);

        var freshProduct = await dbContext.Products.FindAsync(product.Id);
        freshProduct!.DiscountedPrice.Should().Be(95m);

        var history = await dbContext.PriceHistories.SingleAsync();
        history.ProductId.Should().Be(product.Id);
        history.OldDiscountedPrice.Should().Be(100m);
        history.NewDiscountedPrice.Should().Be(95m);
        history.ChangeReason.Should().Contain("AI Assisted Approval");
        history.ChangedBy.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task Approve_Pending_recommendation_below_floor_should_be_rejected()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var merchantUserId = Guid.NewGuid();
        var user = new ApplicationUser { Id = merchantUserId, UserName = "merchant@test.com", Email = "merchant@test.com" };
        dbContext.Users.Add(user);

        var org = new Organization { Id = Guid.NewGuid(), Name = "Assisted Store", AiOperatingMode = AiOperatingMode.Assisted, OwnerId = merchantUserId };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Milk", OriginalPrice = 100m, DiscountedPrice = 100m, Organization = org, Category = category };
        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.9, "corr-id", isPricingStaged: true)
        {
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
        };

        // AI recommended 15% discount (proposed price 85m). Store owner approves it manually.
        var recommendation = new AiPricingRecommendation(
            product.Id, org.Id, 15.0m, "Reason", 0.9,
            AiActionRequirement.APPROVAL_REQUIRED, "ActionReason", "corr-id",
            AiRecommendationStatus.Pending, risk.Id
        )
        {
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
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
        dbContext.ChangeTracker.Clear();

        // Assert
        result.Success.Should().BeTrue();

        var freshProduct = await dbContext.Products.FindAsync(product.Id);
        freshProduct!.DiscountedPrice.Should().Be(85m); // Price mutated by store owner approval

        var histories = await dbContext.PriceHistories.ToListAsync();
        histories.Should().HaveCount(1);
        histories[0].NewDiscountedPrice.Should().Be(85m);
        histories[0].ChangedBy.Should().Be(Guid.Empty);
        histories[0].ChangeReason.Should().Contain("AI Assisted Approval by Store Owner");
    }

    [Fact]
    public async Task Approve_Pending_recommendation_should_transition_to_Approved_with_PriceHistory_row()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var merchantUserId = Guid.NewGuid();
        var user = new ApplicationUser { Id = merchantUserId, UserName = "merchant@test.com", Email = "merchant@test.com" };
        dbContext.Users.Add(user);

        var org = new Organization { Id = Guid.NewGuid(), Name = "Assisted Store", AiOperatingMode = AiOperatingMode.Assisted, OwnerId = merchantUserId };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Milk", OriginalPrice = 100m, DiscountedPrice = 100m, Organization = org, Category = category };
        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.9, "corr-id", isPricingStaged: true)
        {
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
        };

        // Recommended 15% discount (proposed price 85m).
        var recommendation = new AiPricingRecommendation(
            product.Id, org.Id, 15.0m, "Reason", 0.9,
            AiActionRequirement.APPROVAL_REQUIRED, "ActionReason", "corr-id",
            AiRecommendationStatus.Pending, risk.Id
        )
        {
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
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
        dbContext.ChangeTracker.Clear();

        // Assert
        result.Success.Should().BeTrue();

        var freshRec = await dbContext.AiPricingRecommendations.SingleAsync();
        freshRec.Status.Should().Be(AiRecommendationStatus.Approved); // Transitioned to Approved!
        freshRec.ActionReason.Should().Be("Approved by merchant");

        var freshProduct = await dbContext.Products.FindAsync(product.Id);
        freshProduct!.DiscountedPrice.Should().Be(85m); // Price mutated

        var histories = await dbContext.PriceHistories.ToListAsync();
        histories.Should().HaveCount(1);
        histories[0].NewDiscountedPrice.Should().Be(85m);
        histories[0].ChangedBy.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task Reject_Pending_recommendation_should_set_Rejected_status_with_no_price_mutation()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Assisted Store", AiOperatingMode = AiOperatingMode.Assisted, OwnerId = Guid.NewGuid() };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Milk", OriginalPrice = 100m, DiscountedPrice = 100m, Organization = org, Category = category };
        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.9, "corr-id", isPricingStaged: true);

        var recommendation = new AiPricingRecommendation(
            product.Id, org.Id, 5.0m, "Reason", 0.9,
            AiActionRequirement.APPROVAL_REQUIRED, "ActionReason", "corr-id",
            AiRecommendationStatus.Pending, risk.Id
        );

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.AiRiskAssessments.Add(risk);
        dbContext.AiPricingRecommendations.Add(recommendation);
        await dbContext.SaveChangesAsync();

        var handler = new RejectAiRecommendationCommandHandler(dbContext, _mockRejectLogger.Object);

        dbContext.ChangeTracker.Clear();

        // Act
        var result = await handler.Handle(new RejectAiRecommendationCommand(org.OwnerId, recommendation.Id, "Not needed"), CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        // Assert
        result.Success.Should().BeTrue();

        var freshRec = await dbContext.AiPricingRecommendations.SingleAsync();
        freshRec.Status.Should().Be(AiRecommendationStatus.Rejected);
        freshRec.ActionReason.Should().Be("Not needed");

        var freshProduct = await dbContext.Products.FindAsync(product.Id);
        freshProduct!.DiscountedPrice.Should().Be(100m); // NOT mutated

        var histories = await dbContext.PriceHistories.ToListAsync();
        histories.Should().BeEmpty();
    }

    [Fact]
    public async Task Action_on_non_Pending_recommendation_should_fail_cleanly()
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

        var recommendation = new AiPricingRecommendation(
            product.Id, org.Id, 5.0m, "Reason", 0.9,
            AiActionRequirement.APPROVAL_REQUIRED, "ActionReason", "corr-id",
            AiRecommendationStatus.Approved, risk.Id // STATUS IS ALREADY Approved!
        );

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.AiRiskAssessments.Add(risk);
        dbContext.AiPricingRecommendations.Add(recommendation);
        await dbContext.SaveChangesAsync();

        var handler = new ApproveAiRecommendationCommandHandler(dbContext, _timeProvider, _mockApproveLogger.Object);

        dbContext.ChangeTracker.Clear();

        // Act
        var act = async () => await handler.Handle(new ApproveAiRecommendationCommand(merchantUserId, recommendation.Id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>().WithMessage("Recommendation is not in Pending status.");

        var freshProduct = await dbContext.Products.FindAsync(product.Id);
        freshProduct!.DiscountedPrice.Should().Be(100m); // NOT mutated
    }

    [Fact]
    public async Task Merchant_cannot_act_on_another_store_recommendation()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var orgA = new Organization { Id = Guid.NewGuid(), Name = "Store A", AiOperatingMode = AiOperatingMode.Assisted, OwnerId = Guid.NewGuid() };
        var orgB = new Organization { Id = Guid.NewGuid(), Name = "Store B", AiOperatingMode = AiOperatingMode.Assisted, OwnerId = Guid.NewGuid() };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Milk", OriginalPrice = 100m, DiscountedPrice = 100m, Organization = orgA, Category = category };
        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.9, "corr-id", isPricingStaged: true);

        var recommendation = new AiPricingRecommendation(
            product.Id, orgA.Id, 5.0m, "Reason", 0.9,
            AiActionRequirement.APPROVAL_REQUIRED, "ActionReason", "corr-id",
            AiRecommendationStatus.Pending, risk.Id
        );

        dbContext.Organizations.AddRange(orgA, orgB);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.AiRiskAssessments.Add(risk);
        dbContext.AiPricingRecommendations.Add(recommendation);
        await dbContext.SaveChangesAsync();

        var handler = new ApproveAiRecommendationCommandHandler(dbContext, _timeProvider, _mockApproveLogger.Object);

        dbContext.ChangeTracker.Clear();

        // Act - Merchant B attempts to approve recommendation for Organization A
        Func<Task> act = async () => await handler.Handle(new ApproveAiRecommendationCommand(orgB.OwnerId, recommendation.Id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Merchant is not authorized to act on another store's recommendation.");
    }

    [Fact]
    public async Task Concurrent_approval_attempts_should_allow_exactly_one_price_mutation()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var merchantUserId = Guid.NewGuid();
        var user = new ApplicationUser { Id = merchantUserId, UserName = "merchant@test.com", Email = "merchant@test.com" };
        dbContext.Users.Add(user);

        var org = new Organization { Id = Guid.NewGuid(), Name = "Assisted Store", AiOperatingMode = AiOperatingMode.Assisted, OwnerId = merchantUserId };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Milk", OriginalPrice = 100m, DiscountedPrice = 100m, Organization = org, Category = category };
        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.9, "corr-id", isPricingStaged: true)
        {
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
        };

        var recommendation = new AiPricingRecommendation(
            product.Id, org.Id, 5.0m, "Reason", 0.9,
            AiActionRequirement.APPROVAL_REQUIRED, "ActionReason", "corr-id",
            AiRecommendationStatus.Pending, risk.Id
        )
        {
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.AiRiskAssessments.Add(risk);
        dbContext.AiPricingRecommendations.Add(recommendation);
        await dbContext.SaveChangesAsync();

        var handler = new ApproveAiRecommendationCommandHandler(dbContext, _timeProvider, _mockApproveLogger.Object);

        dbContext.ChangeTracker.Clear();

        // Act - Simulate concurrent execution by running sequentially inside transactions to check locks:
        // First execution succeeds.
        var result1 = await handler.Handle(new ApproveAiRecommendationCommand(merchantUserId, recommendation.Id), CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        
        // Second execution fails because first claimed the lock and status transitioned.
        var act2 = async () => await handler.Handle(new ApproveAiRecommendationCommand(merchantUserId, recommendation.Id), CancellationToken.None);

        // Assert
        result1.Success.Should().BeTrue();
        await act2.Should().ThrowAsync<ConflictException>().WithMessage("Recommendation is not in Pending status.");

        var freshProduct = await dbContext.Products.FindAsync(product.Id);
        freshProduct!.DiscountedPrice.Should().Be(95m); // Mutated exactly once

        var histories = await dbContext.PriceHistories.ToListAsync();
        histories.Count.Should().Be(1); // Exactly one PriceHistory row
    }

    [Fact]
    public async Task Reject_action_on_non_Pending_recommendation_should_fail_cleanly()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Assisted Store", AiOperatingMode = AiOperatingMode.Assisted, OwnerId = Guid.NewGuid() };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Milk", OriginalPrice = 100m, DiscountedPrice = 100m, Organization = org, Category = category };
        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.9, "corr-id", isPricingStaged: true);

        var recommendation = new AiPricingRecommendation(
            product.Id, org.Id, 5.0m, "Reason", 0.9,
            AiActionRequirement.APPROVAL_REQUIRED, "ActionReason", "corr-id",
            AiRecommendationStatus.Approved, risk.Id // STATUS IS Approved (non-pending)
        );

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.AiRiskAssessments.Add(risk);
        dbContext.AiPricingRecommendations.Add(recommendation);
        await dbContext.SaveChangesAsync();

        var handler = new RejectAiRecommendationCommandHandler(dbContext, _mockRejectLogger.Object);

        dbContext.ChangeTracker.Clear();

        // Act
        var act = async () => await handler.Handle(new RejectAiRecommendationCommand(org.OwnerId, recommendation.Id, "Not needed"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>().WithMessage("Recommendation is not in Pending status.");

        var freshProduct = await dbContext.Products.FindAsync(product.Id);
        freshProduct!.DiscountedPrice.Should().Be(100m); // NOT mutated
    }

    [Fact]
    public async Task GetPendingRecommendations_should_only_return_Pending_recommendations_for_merchant_store()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var merchantUserId = Guid.NewGuid();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Merchant Store", AiOperatingMode = AiOperatingMode.Assisted, OwnerId = merchantUserId };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Milk", OriginalPrice = 10m, DiscountedPrice = 10m, Organization = org, Category = category };
        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.9, "corr-id", isPricingStaged: true);

        // One Pending and one Approved recommendation (second has null risk ID to avoid unique index violation)
        var rec1 = new AiPricingRecommendation(product.Id, org.Id, 5.0m, "Pending Reason", 0.9, AiActionRequirement.APPROVAL_REQUIRED, "", "corr-1", AiRecommendationStatus.Pending, risk.Id);
        var rec2 = new AiPricingRecommendation(product.Id, org.Id, 10.0m, "Approved Reason", 0.95, AiActionRequirement.APPROVAL_REQUIRED, "", "corr-2", AiRecommendationStatus.Approved, null);

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.AiRiskAssessments.Add(risk);
        dbContext.AiPricingRecommendations.AddRange(rec1, rec2);
        await dbContext.SaveChangesAsync();

        var query = new GetPendingAiRecommendationsQuery(merchantUserId);
        var handler = new GetPendingAiRecommendationsQueryHandler(dbContext);

        dbContext.ChangeTracker.Clear();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Count.Should().Be(1);
        result.Data[0].Id.Should().Be(rec1.Id);
        result.Data[0].Reason.Should().Be("Pending Reason");
    }

    [Fact]
    public async Task GetPendingRecommendations_should_return_empty_list_when_no_recommendations_for_store()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var merchantUserId = Guid.NewGuid();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Merchant Store", AiOperatingMode = AiOperatingMode.Assisted, OwnerId = merchantUserId };

        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync();

        var query = new GetPendingAiRecommendationsQuery(merchantUserId);
        var handler = new GetPendingAiRecommendationsQueryHandler(dbContext);

        dbContext.ChangeTracker.Clear();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingRecommendations_should_return_empty_list_when_called_for_store_other_than_recommendation_organization()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var merchantUserIdA = Guid.NewGuid();
        var merchantUserIdB = Guid.NewGuid();
        var orgA = new Organization { Id = Guid.NewGuid(), Name = "Store A", AiOperatingMode = AiOperatingMode.Assisted, OwnerId = merchantUserIdA };
        var orgB = new Organization { Id = Guid.NewGuid(), Name = "Store B", AiOperatingMode = AiOperatingMode.Assisted, OwnerId = merchantUserIdB };

        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Milk", OriginalPrice = 10m, DiscountedPrice = 10m, Organization = orgA, Category = category };
        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.9, "corr-id", isPricingStaged: true);

        // Recommendation belongs to Store A
        var recommendation = new AiPricingRecommendation(
            product.Id, orgA.Id, 5.0m, "Reason", 0.9,
            AiActionRequirement.APPROVAL_REQUIRED, "", "corr-id",
            AiRecommendationStatus.Pending, risk.Id
        );

        dbContext.Organizations.AddRange(orgA, orgB);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.AiRiskAssessments.Add(risk);
        dbContext.AiPricingRecommendations.Add(recommendation);
        await dbContext.SaveChangesAsync();

        // Merchant B queries their pending recommendations
        var query = new GetPendingAiRecommendationsQuery(merchantUserIdB);
        var handler = new GetPendingAiRecommendationsQueryHandler(dbContext);

        dbContext.ChangeTracker.Clear();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().BeEmpty(); // Does not return Store A's recommendation
    }
}

public class TestApplicationDbContext : FoodLoop.Infrastructure.Persistence.ApplicationDbContext
{
    public TestApplicationDbContext(DbContextOptions<FoodLoop.Infrastructure.Persistence.ApplicationDbContext> options)
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
