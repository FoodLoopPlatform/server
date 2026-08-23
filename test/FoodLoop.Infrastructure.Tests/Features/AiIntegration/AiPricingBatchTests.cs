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
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.AiIntegration;

public class AiPricingBatchTests
{
    private readonly Mock<IAiServiceClient> _mockAiClient;
    private readonly Mock<ICorrelationIdAccessor> _mockCorrelationAccessor;
    private readonly Mock<ILogger<RunPricingBatchCommandHandler>> _mockLogger;
    private readonly TimeProvider _timeProvider;

    public AiPricingBatchTests()
    {
        _mockAiClient = new Mock<IAiServiceClient>();
        _mockCorrelationAccessor = new Mock<ICorrelationIdAccessor>();
        _mockLogger = new Mock<ILogger<RunPricingBatchCommandHandler>>();
        _timeProvider = TimeProvider.System;

        _mockCorrelationAccessor.Setup(x => x.GetCorrelationId()).Returns("test-correlation-id");
    }

    private RunPricingBatchCommandHandler CreateHandler(IApplicationDbContext dbContext, Microsoft.Extensions.Options.IOptions<FoodLoop.Infrastructure.Options.AiServiceOptions>? options = null)
    {
        return new RunPricingBatchCommandHandler(
            dbContext,
            _mockAiClient.Object,
            _mockCorrelationAccessor.Object,
            _timeProvider,
            _mockLogger.Object,
            options
        );
    }

    [Fact]
    public async Task Handle_should_group_candidates_correctly_by_store()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var orgA = new Organization { Id = Guid.NewGuid(), Name = "Store A", AiOperatingMode = AiOperatingMode.Assisted };
        var orgB = new Organization { Id = Guid.NewGuid(), Name = "Store B", AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };

        var prodA = new Product { Id = Guid.NewGuid(), Title = "ProdA", OriginalPrice = 10m, DiscountedPrice = 10m, Organization = orgA, Category = category };
        var prodB = new Product { Id = Guid.NewGuid(), Title = "ProdB", OriginalPrice = 20m, DiscountedPrice = 20m, Organization = orgB, Category = category };

        var riskA = new AiRiskAssessment(prodA.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Risk A", 0.9, "corr-a", isPricingStaged: true);
        var riskB = new AiRiskAssessment(prodB.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Risk B", 0.9, "corr-b", isPricingStaged: true);

        dbContext.Organizations.AddRange(orgA, orgB);
        dbContext.Categories.Add(category);
        dbContext.Products.AddRange(prodA, prodB);
        dbContext.AiRiskAssessments.AddRange(riskA, riskB);
        await dbContext.SaveChangesAsync();

        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.Is<PricingBatchRequestDto>(r => r.StoreId == orgA.Id.ToString()), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingBatchResponseDto(orgA.Id.ToString(), new List<PricingDecisionDto>
            {
                new PricingDecisionDto(prodA.Id.ToString(), 10.0, "Reason A", 0.9, "APPROVAL_REQUIRED", "Reason")
            }));

        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.Is<PricingBatchRequestDto>(r => r.StoreId == orgB.Id.ToString()), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingBatchResponseDto(orgB.Id.ToString(), new List<PricingDecisionDto>
            {
                new PricingDecisionDto(prodB.Id.ToString(), 15.0, "Reason B", 0.95, "APPROVAL_REQUIRED", "Reason")
            }));

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        _mockAiClient.Verify(x => x.RecommendPricingAsync(It.Is<PricingBatchRequestDto>(r => r.StoreId == orgA.Id.ToString()), It.IsAny<CancellationToken>()), Times.Once);
        _mockAiClient.Verify(x => x.RecommendPricingAsync(It.Is<PricingBatchRequestDto>(r => r.StoreId == orgB.Id.ToString()), It.IsAny<CancellationToken>()), Times.Once);

        var recommendations = await dbContext.AiPricingRecommendations.ToListAsync();
        recommendations.Count.Should().Be(2);
        recommendations.Any(r => r.ProductId == prodA.Id).Should().BeTrue();
        recommendations.Any(r => r.ProductId == prodB.Id).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_should_persist_Pending_recommendation_and_not_mutate_price_for_Assisted_mode()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Assisted Store", AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Milk", OriginalPrice = 10m, DiscountedPrice = 10m, Organization = org, Category = category };
        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.9, "corr-id", isPricingStaged: true);

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.AiRiskAssessments.Add(risk);
        await dbContext.SaveChangesAsync();

        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.IsAny<PricingBatchRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingBatchResponseDto(org.Id.ToString(), new List<PricingDecisionDto>
            {
                new PricingDecisionDto(product.Id.ToString(), 10.0, "Decide Milk", 0.85, "APPROVAL_REQUIRED", "Reason")
            }));

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        var recommendation = await dbContext.AiPricingRecommendations.SingleAsync();
        recommendation.Status.Should().Be(AiRecommendationStatus.Pending);
        recommendation.DiscountPercentage.Should().Be(10.0m);
        recommendation.RiskAssessmentId.Should().Be(risk.Id);

        var freshProduct = await dbContext.Products.FindAsync(product.Id);
        freshProduct!.DiscountedPrice.Should().Be(10m); // MUST NOT MUTATE original/discounted price
    }

    [Fact]
    public async Task Handle_should_apply_discount_and_set_AutoExecuted_for_Autonomous_mode_when_above_floor()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Auto Store", AiOperatingMode = AiOperatingMode.Autonomous };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Milk", OriginalPrice = 10m, DiscountedPrice = 10m, Organization = org, Category = category };
        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.9, "corr-id", isPricingStaged: true)
        {
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
        };

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

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        var recommendation = await dbContext.AiPricingRecommendations.SingleAsync();
        recommendation.Status.Should().Be(AiRecommendationStatus.AutoExecuted);
        recommendation.DiscountPercentage.Should().Be(5.0m);

        var freshProduct = await dbContext.Products.FindAsync(product.Id);
        freshProduct!.DiscountedPrice.Should().Be(9.5m); // 5% off 10m is 9.5m
    }

    [Fact]
    public async Task Handle_should_reject_and_not_mutate_price_for_Autonomous_mode_when_below_floor()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Auto Store", AiOperatingMode = AiOperatingMode.Autonomous };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        
        // Original price 10m. Default DynamicAi price floor is 70% (7.00m)
        var product = new Product { Id = Guid.NewGuid(), Title = "Milk", OriginalPrice = 10m, DiscountedPrice = 10m, Organization = org, Category = category };
        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.9, "corr-id", isPricingStaged: true)
        {
            SnapshotOriginalPrice = product.OriginalPrice,
            SnapshotQuantityAvailable = product.QuantityAvailable,
            SnapshotProductStatus = product.Status
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.AiRiskAssessments.Add(risk);
        await dbContext.SaveChangesAsync();

        // AI recommends a 15% discount -> proposed price is 8.50m, which falls below the 90% floor (9.00m)
        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.IsAny<PricingBatchRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingBatchResponseDto(org.Id.ToString(), new List<PricingDecisionDto>
            {
                new PricingDecisionDto(product.Id.ToString(), 15.0, "Decide Milk", 0.95, "AUTOMATIC_EXECUTION_ELIGIBLE", "Reason")
            }));

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        var recommendation = await dbContext.AiPricingRecommendations.SingleAsync();
        recommendation.Status.Should().Be(AiRecommendationStatus.Rejected);
        recommendation.Reason.Should().Contain("Price Floor Violation");

        var freshProduct = await dbContext.Products.FindAsync(product.Id);
        freshProduct!.DiscountedPrice.Should().Be(10m); // NOT mutated
    }

    [Fact]
    public async Task Handle_should_defensively_skip_Manual_mode_stores_even_if_staged()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Manual Store", AiOperatingMode = AiOperatingMode.Manual };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Milk", OriginalPrice = 10m, DiscountedPrice = 10m, Organization = org, Category = category };
        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.9, "corr-id", isPricingStaged: true);

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.AiRiskAssessments.Add(risk);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        _mockAiClient.Verify(x => x.RecommendPricingAsync(It.IsAny<PricingBatchRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
        
        var recommendations = await dbContext.AiPricingRecommendations.ToListAsync();
        recommendations.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_should_not_process_duplicate_staged_candidates()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Assisted Store", AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Milk", OriginalPrice = 10m, DiscountedPrice = 10m, Organization = org, Category = category };
        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.9, "corr-id", isPricingStaged: true);

        // Pre-create an existing recommendation for this assessment
        var existingRecommendation = new AiPricingRecommendation(
            product.Id, org.Id, 10m, "Already done", 0.9,
            AiActionRequirement.APPROVAL_REQUIRED, "Reason", "corr-id",
            AiRecommendationStatus.Pending, risk.Id
        );

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.AiRiskAssessments.Add(risk);
        dbContext.AiPricingRecommendations.Add(existingRecommendation);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        _mockAiClient.Verify(x => x.RecommendPricingAsync(It.IsAny<PricingBatchRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_should_be_resilient_to_individual_store_failures()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var orgA = new Organization { Id = Guid.NewGuid(), Name = "Store A", AiOperatingMode = AiOperatingMode.Assisted };
        var orgB = new Organization { Id = Guid.NewGuid(), Name = "Store B", AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };

        var prodA = new Product { Id = Guid.NewGuid(), Title = "ProdA", OriginalPrice = 10m, DiscountedPrice = 10m, Organization = orgA, Category = category };
        var prodB = new Product { Id = Guid.NewGuid(), Title = "ProdB", OriginalPrice = 20m, DiscountedPrice = 20m, Organization = orgB, Category = category };

        var riskA = new AiRiskAssessment(prodA.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Risk A", 0.9, "corr-a", isPricingStaged: true);
        var riskB = new AiRiskAssessment(prodB.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Risk B", 0.9, "corr-b", isPricingStaged: true);

        dbContext.Organizations.AddRange(orgA, orgB);
        dbContext.Categories.Add(category);
        dbContext.Products.AddRange(prodA, prodB);
        dbContext.AiRiskAssessments.AddRange(riskA, riskB);
        await dbContext.SaveChangesAsync();

        // Store A throws exception
        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.Is<PricingBatchRequestDto>(r => r.StoreId == orgA.Id.ToString()), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Server Down"));

        // Store B succeeds
        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.Is<PricingBatchRequestDto>(r => r.StoreId == orgB.Id.ToString()), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingBatchResponseDto(orgB.Id.ToString(), new List<PricingDecisionDto>
            {
                new PricingDecisionDto(prodB.Id.ToString(), 15.0, "Reason B", 0.95, "APPROVAL_REQUIRED", "Reason")
            }));

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue(); // Should not fail overall scan
        
        var recommendations = await dbContext.AiPricingRecommendations.ToListAsync();
        recommendations.Should().ContainSingle(); // Store B is updated, Store A is omitted
        recommendations.First().ProductId.Should().Be(prodB.Id);
    }

    [Fact]
    public async Task Handle_should_handle_AiServiceContractException_gracefully_per_store()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Assisted Store", AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Milk", OriginalPrice = 10m, DiscountedPrice = 10m, Organization = org, Category = category };
        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.9, "corr-id", isPricingStaged: true);

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.AiRiskAssessments.Add(risk);
        await dbContext.SaveChangesAsync();

        // Setup mock to throw AiServiceContractException as a validation contract violation
        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.IsAny<PricingBatchRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiServiceContractException("Validation failure - out of bounds discount"));

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue(); // Gracefully handled at store level, command succeeds

        var recommendations = await dbContext.AiPricingRecommendations.ToListAsync();
        recommendations.Should().BeEmpty(); // No recommendation persisted for the bad store
    }

    [Fact]
    public async Task Handle_should_continue_other_stores_when_one_store_returns_contract_violation()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var orgA = new Organization { Id = Guid.NewGuid(), Name = "Store A", AiOperatingMode = AiOperatingMode.Assisted };
        var orgB = new Organization { Id = Guid.NewGuid(), Name = "Store B", AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };

        var prodA = new Product { Id = Guid.NewGuid(), Title = "ProdA", OriginalPrice = 10m, DiscountedPrice = 10m, Organization = orgA, Category = category };
        var prodB = new Product { Id = Guid.NewGuid(), Title = "ProdB", OriginalPrice = 20m, DiscountedPrice = 20m, Organization = orgB, Category = category };

        var riskA = new AiRiskAssessment(prodA.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Risk A", 0.9, "corr-a", isPricingStaged: true);
        var riskB = new AiRiskAssessment(prodB.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Risk B", 0.9, "corr-b", isPricingStaged: true);

        dbContext.Organizations.AddRange(orgA, orgB);
        dbContext.Categories.Add(category);
        dbContext.Products.AddRange(prodA, prodB);
        dbContext.AiRiskAssessments.AddRange(riskA, riskB);
        await dbContext.SaveChangesAsync();

        // Store A throws contract exception
        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.Is<PricingBatchRequestDto>(r => r.StoreId == orgA.Id.ToString()), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiServiceContractException("Validation failure - out of bounds discount"));

        // Store B succeeds
        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.Is<PricingBatchRequestDto>(r => r.StoreId == orgB.Id.ToString()), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingBatchResponseDto(orgB.Id.ToString(), new List<PricingDecisionDto>
            {
                new PricingDecisionDto(prodB.Id.ToString(), 15.0, "Reason B", 0.95, "APPROVAL_REQUIRED", "Reason")
            }));

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        var recommendations = await dbContext.AiPricingRecommendations.ToListAsync();
        recommendations.Should().ContainSingle(); // Store B persisted, Store A skipped
        recommendations.First().ProductId.Should().Be(prodB.Id);
    }

    [Fact]
    public async Task Database_should_enforce_uniqueness_on_RiskAssessmentId_constraint()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Assisted Store", AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Milk", OriginalPrice = 10m, DiscountedPrice = 10m, Organization = org, Category = category };
        var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry", 0.9, "corr-id", isPricingStaged: true);

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.AiRiskAssessments.Add(risk);
        await dbContext.SaveChangesAsync();

        var rec1 = new AiPricingRecommendation(
            product.Id, org.Id, 10m, "First", 0.9,
            AiActionRequirement.APPROVAL_REQUIRED, "Reason", "corr-id",
            AiRecommendationStatus.Pending, risk.Id
        );

        var rec2 = new AiPricingRecommendation(
            product.Id, org.Id, 15m, "Second", 0.9,
            AiActionRequirement.APPROVAL_REQUIRED, "Reason", "corr-id",
            AiRecommendationStatus.Pending, risk.Id
        );

        dbContext.AiPricingRecommendations.Add(rec1);
        await dbContext.SaveChangesAsync();

        // Act
        dbContext.AiPricingRecommendations.Add(rec2);
        Func<Task> act = async () => await dbContext.SaveChangesAsync();

        // Assert - EF throws DbUpdateException due to unique constraint index violation on SQLite
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    private FoodLoop.Infrastructure.Persistence.ApplicationDbContext CreateSqliteContext()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<FoodLoop.Infrastructure.Persistence.ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new FoodLoop.Infrastructure.Persistence.ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task Handle_should_chunk_batches_larger_than_MaxPricingBatchSize_into_multiple_requests()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Chunk Store", AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Snacks" };
        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);

        var products = new List<Product>();
        var risks = new List<AiRiskAssessment>();

        for (int i = 1; i <= 75; i++)
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Title = $"Snack {i}",
                OriginalPrice = 10.00m,
                DiscountedPrice = 10.00m,
                QuantityAvailable = 10,
                OrganizationId = org.Id,
                Organization = org,
                CategoryId = category.Id,
                Category = category
            };
            products.Add(product);
            dbContext.Products.Add(product);

            var risk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "High risk", 0.9, "corr", isPricingStaged: true)
            {
                SnapshotOriginalPrice = 10.00m,
                SnapshotQuantityAvailable = 10,
                SnapshotProductStatus = ProductStatus.Active
            };
            risks.Add(risk);
            dbContext.AiRiskAssessments.Add(risk);
        }

        await dbContext.SaveChangesAsync();

        // Expect 2 calls: first chunk has 50 products, second has 25 products
        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.Is<PricingBatchRequestDto>(r => r.Products.Count == 50), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PricingBatchRequestDto req, CancellationToken ct) =>
            {
                var decisions = req.Products.Select(p => new PricingDecisionDto(p.ProductId, 10.0, "Reason", 0.9, "APPROVAL_REQUIRED", "Assisted Mode")).ToList();
                return new PricingBatchResponseDto(org.Id.ToString(), decisions);
            });

        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.Is<PricingBatchRequestDto>(r => r.Products.Count == 25), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PricingBatchRequestDto req, CancellationToken ct) =>
            {
                var decisions = req.Products.Select(p => new PricingDecisionDto(p.ProductId, 10.0, "Reason", 0.9, "APPROVAL_REQUIRED", "Assisted Mode")).ToList();
                return new PricingBatchResponseDto(org.Id.ToString(), decisions);
            });

        var optionsMock = new Mock<Microsoft.Extensions.Options.IOptions<FoodLoop.Infrastructure.Options.AiServiceOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new FoodLoop.Infrastructure.Options.AiServiceOptions { MaxPricingBatchSize = 50 });

        var handler = CreateHandler(dbContext, optionsMock.Object);

        // Act
        var result = await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        _mockAiClient.Verify(x => x.RecommendPricingAsync(It.Is<PricingBatchRequestDto>(r => r.Products.Count == 50), It.IsAny<CancellationToken>()), Times.Once);
        _mockAiClient.Verify(x => x.RecommendPricingAsync(It.Is<PricingBatchRequestDto>(r => r.Products.Count == 25), It.IsAny<CancellationToken>()), Times.Once);

        var recommendations = await dbContext.AiPricingRecommendations.ToListAsync();
        recommendations.Count.Should().Be(75);
    }

    [Fact]
    public async Task RunPricingBatch_in_assisted_mode_should_supersede_older_pending_recommendations_for_same_product()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Assisted Store", AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product { Id = Guid.NewGuid(), Title = "Milk", OriginalPrice = 50m, DiscountedPrice = 50m, Organization = org, Category = category, Status = ProductStatus.Active, QuantityAvailable = 10 };

        var oldRisk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry 1", 0.85, "corr-old", isPricingStaged: false);
        var oldRec = new AiPricingRecommendation(
            product.Id, org.Id, 5.0m, "5% discount", 0.85,
            AiActionRequirement.APPROVAL_REQUIRED, "Assisted", "corr-old",
            AiRecommendationStatus.Pending, oldRisk.Id
        );

        var newRisk = new AiRiskAssessment(product.Id, AiRiskLevel.HIGH, AiRoute.PRICING, "Nearing Expiry 2", 0.95, "corr-new", isPricingStaged: true)
        {
            SnapshotOriginalPrice = 50.00m,
            SnapshotQuantityAvailable = 10,
            SnapshotProductStatus = ProductStatus.Active
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.AiRiskAssessments.AddRange(oldRisk, newRisk);
        dbContext.AiPricingRecommendations.Add(oldRec);
        await dbContext.SaveChangesAsync();

        _mockAiClient.Setup(x => x.RecommendPricingAsync(It.IsAny<PricingBatchRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingBatchResponseDto(org.Id.ToString(), new List<PricingDecisionDto>
            {
                new PricingDecisionDto(product.Id.ToString(), 10.0, "10% markdown needed", 0.95, "APPROVAL_REQUIRED", "Assisted Mode")
            }));

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunPricingBatchCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        var recommendations = await dbContext.AiPricingRecommendations.OrderBy(r => r.CreatedAt).ToListAsync();
        recommendations.Should().HaveCount(2);

        // The older recommendation must have been transitioned to Rejected (superseded)
        var updatedOldRec = recommendations.First(r => r.Id == oldRec.Id);
        updatedOldRec.Status.Should().Be(AiRecommendationStatus.Rejected);
        updatedOldRec.ActionReason.Should().Be("Superseded by newer AI pricing cycle");

        // The new recommendation must be Pending
        var freshRec = recommendations.First(r => r.Id != oldRec.Id);
        freshRec.Status.Should().Be(AiRecommendationStatus.Pending);
        freshRec.DiscountPercentage.Should().Be(10.0m);
    }
}
