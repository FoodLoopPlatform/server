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
using FoodLoop.Application.Common.Exceptions.AI;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Interfaces.AI;
using FoodLoop.Application.Features.AiIntegration.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.AiIntegration;
using FoodLoop.Infrastructure.Features.AiIntegration.Commands;
using FoodLoop.Infrastructure.Integrations.AiService;
using FoodLoop.Infrastructure.Options;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.AiIntegration;

public class HistoricalIngestionTests
{
    private readonly Mock<IAiServiceClient> _mockAiClient;
    private readonly Mock<ICorrelationIdAccessor> _mockCorrelationAccessor;
    private readonly Mock<ILogger<RunHistoricalIngestionCommandHandler>> _mockLogger;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly HistoricalIngestionOptions _options;

    public HistoricalIngestionTests()
    {
        _mockAiClient = new Mock<IAiServiceClient>();
        _mockCorrelationAccessor = new Mock<ICorrelationIdAccessor>();
        _mockLogger = new Mock<ILogger<RunHistoricalIngestionCommandHandler>>();
        _mockTimeProvider = new Mock<TimeProvider>();
        
        _options = new HistoricalIngestionOptions
        {
            IntervalMinutes = 60,
            BatchSize = 2
        };

        _mockCorrelationAccessor.Setup(x => x.GetCorrelationId()).Returns("test-correlation-id");
        
        var utcNow = new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);
        _mockTimeProvider.Setup(x => x.GetUtcNow()).Returns(utcNow);

        // Default setup: assume all submitted events are successfully ingested
        _mockAiClient.Setup(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HistoricalIngestionRequestDto req, CancellationToken ct) =>
            {
                var docIds = req.Events.Select(e => e.EventId).ToList();
                return new HistoricalIngestionResponseDto(docIds.Count, docIds.Count, 0, docIds);
            });
    }

    private RunHistoricalIngestionCommandHandler CreateHandler(IApplicationDbContext dbContext)
    {
        var mockOptions = new Mock<IOptions<HistoricalIngestionOptions>>();
        mockOptions.Setup(x => x.Value).Returns(_options);

        return new RunHistoricalIngestionCommandHandler(
            dbContext,
            _mockAiClient.Object,
            _mockCorrelationAccessor.Object,
            _mockTimeProvider.Object,
            mockOptions.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_should_derive_outcomes_correctly_for_all_representative_states()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Store A" };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };

        var today = DateOnly.FromDateTime(_mockTimeProvider.Object.GetUtcNow().DateTime);

        // SOLD_OUT: quantity == 0
        var pSoldOut = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Sold Out Product",
            OriginalPrice = 100m,
            DiscountedPrice = 85m,
            QuantityAvailable = 0,
            ExpirationDate = today.AddDays(5),
            Organization = org,
            Category = category,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-10)
        };

        // PARTIALLY_SOLD: quantity > 0, units sold > 0, expired
        var pPartiallySold = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Partially Sold Product",
            OriginalPrice = 100m,
            DiscountedPrice = 85m,
            QuantityAvailable = 5,
            ExpirationDate = today.AddDays(-2),
            Organization = org,
            Category = category,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-10)
        };

        // EXPIRED: quantity > 0, units sold == 0, expired
        var pExpired = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Expired Product",
            OriginalPrice = 100m,
            DiscountedPrice = 85m,
            QuantityAvailable = 10,
            ExpirationDate = today.AddDays(-2),
            Organization = org,
            Category = category,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-10)
        };

        // UNSOLD: quantity > 0, units sold == 0, IsDeleted == true
        var pUnsold = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Unsold Product",
            OriginalPrice = 100m,
            DiscountedPrice = 85m,
            QuantityAvailable = 10,
            ExpirationDate = today.AddDays(5),
            Organization = org,
            Category = category,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-10),
            IsDeleted = true
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.AddRange(pSoldOut, pPartiallySold, pExpired, pUnsold);

        // Add a sale for pSoldOut and pPartiallySold
        var order1 = new Order { Id = Guid.NewGuid(), PaymentStatus = PaymentStatus.Paid, CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-4) };
        order1.Items.Add(new OrderItem { ProductId = pSoldOut.Id, Quantity = 10, UnitPrice = 85m });
        order1.Items.Add(new OrderItem { ProductId = pPartiallySold.Id, Quantity = 3, UnitPrice = 85m });
        dbContext.Orders.Add(order1);

        // Add a price history discountEvent for pPartiallySold
        dbContext.PriceHistories.Add(new PriceHistory
        {
            Id = Guid.NewGuid(),
            ProductId = pPartiallySold.Id,
            OldOriginalPrice = 100m,
            OldDiscountedPrice = 100m,
            NewOriginalPrice = 100m,
            NewDiscountedPrice = 85m,
            ChangeReason = "AI Assisted Approval",
            ChangedBy = Guid.Empty,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-5)
        });

        await dbContext.SaveChangesAsync();

        HistoricalIngestionRequestDto? capturedRequest = null;
        _mockAiClient.Setup(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<HistoricalIngestionRequestDto, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HistoricalIngestionResponseDto(4, 4, 0, new List<string>()));

        _options.BatchSize = 10;
        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunHistoricalIngestionCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Events.Count.Should().Be(4);

        var eventSoldOut = capturedRequest.Events.First(e => e.ProductId == pSoldOut.Id.ToString());
        eventSoldOut.Outcome.Should().Be("SOLD_OUT");
        eventSoldOut.Quantity.Should().Be(10); // startingQuantity = 0 + 10 = 10

        var eventPartiallySold = capturedRequest.Events.First(e => e.ProductId == pPartiallySold.Id.ToString());
        eventPartiallySold.Outcome.Should().Be("PARTIALLY_SOLD");
        eventPartiallySold.Quantity.Should().Be(8); // startingQuantity = 5 + 3 = 8
        eventPartiallySold.UnitsSoldAfterDiscount.Should().Be(3); // sale was at AddDays(-4), discount was at AddDays(-5)
        eventPartiallySold.DiscountPercentage.Should().Be(15.0); // (100 - 85)/100 * 100 = 15%

        var eventExpired = capturedRequest.Events.First(e => e.ProductId == pExpired.Id.ToString());
        eventExpired.Outcome.Should().Be("EXPIRED");
        eventExpired.Quantity.Should().Be(10);

        var eventUnsold = capturedRequest.Events.First(e => e.ProductId == pUnsold.Id.ToString());
        eventUnsold.Outcome.Should().Be("UNSOLD");
        eventUnsold.Quantity.Should().Be(10);

        // Verify episodes are stored in database
        var dbEpisodes = await dbContext.ProductPricingEpisodes.ToListAsync();
        dbEpisodes.Count.Should().Be(4);
        dbEpisodes.Any(pe => pe.ProductId == pSoldOut.Id && pe.Outcome == "SOLD_OUT" && pe.IngestionCorrelationId == "test-correlation-id").Should().BeTrue();
        dbEpisodes.Any(pe => pe.ProductId == pPartiallySold.Id && pe.Outcome == "PARTIALLY_SOLD").Should().BeTrue();
        dbEpisodes.Any(pe => pe.ProductId == pExpired.Id && pe.Outcome == "EXPIRED").Should().BeTrue();
        dbEpisodes.Any(pe => pe.ProductId == pUnsold.Id && pe.Outcome == "UNSOLD").Should().BeTrue();
    }

    [Fact]
    public async Task Handle_should_exclude_already_ingested_products()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Store A" };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var today = DateOnly.FromDateTime(_mockTimeProvider.Object.GetUtcNow().DateTime);

        var pIngested = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Already Ingested Product",
            OriginalPrice = 100m,
            DiscountedPrice = 85m,
            QuantityAvailable = 0,
            ExpirationDate = today.AddDays(5),
            Organization = org,
            Category = category,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-10)
        };

        var pNew = new Product
        {
            Id = Guid.NewGuid(),
            Title = "New Product",
            OriginalPrice = 100m,
            DiscountedPrice = 85m,
            QuantityAvailable = 0,
            ExpirationDate = today.AddDays(5),
            Organization = org,
            Category = category,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-10)
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.AddRange(pIngested, pNew);

        // Add already ingested episode for pIngested
        dbContext.ProductPricingEpisodes.Add(new ProductPricingEpisode
        {
            Id = Guid.NewGuid(),
            ProductId = pIngested.Id,
            EventId = $"ep-{pIngested.Id}-nodisc",
            RecordedAt = pIngested.CreatedAt,
            IngestedAt = DateTimeOffset.UtcNow,
            IngestionCorrelationId = "existing-id",
            Outcome = "UNSOLD"
        });

        await dbContext.SaveChangesAsync();

        HistoricalIngestionRequestDto? capturedRequest = null;
        _mockAiClient.Setup(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<HistoricalIngestionRequestDto, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HistoricalIngestionResponseDto(1, 1, 0, new List<string>()));

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunHistoricalIngestionCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Events.Count.Should().Be(1);
        capturedRequest.Events[0].ProductId.Should().Be(pNew.Id.ToString());
    }

    [Fact]
    public async Task Handle_should_batch_correctly_based_on_BatchSize()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Store A" };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var today = DateOnly.FromDateTime(_mockTimeProvider.Object.GetUtcNow().DateTime);

        // Add 3 products
        for (int i = 0; i < 3; i++)
        {
            dbContext.Products.Add(new Product
            {
                Id = Guid.NewGuid(),
                Title = $"Prod {i}",
                OriginalPrice = 10m,
                DiscountedPrice = 10m,
                QuantityAvailable = 0,
                ExpirationDate = today.AddDays(5),
                Organization = org,
                Category = category,
                CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-10)
            });
        }

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();

        _mockAiClient.Setup(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HistoricalIngestionResponseDto(2, 2, 0, new List<string>()));

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunHistoricalIngestionCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        _mockAiClient.Verify(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

        var dbEpisodes = await dbContext.ProductPricingEpisodes.ToListAsync();
        dbEpisodes.Count.Should().Be(3);
    }

    [Fact]
    public async Task Handle_should_handle_out_of_bounds_discount_percentages_safely()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Store A" };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var today = DateOnly.FromDateTime(_mockTimeProvider.Object.GetUtcNow().DateTime);

        // Product A: 20% discount (outside 15% schema limit) -> discountPercentage = 20.0
        var prodA = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Prod A (20% discount)",
            OriginalPrice = 100m,
            DiscountedPrice = 80m,
            QuantityAvailable = 0,
            ExpirationDate = today.AddDays(5),
            Organization = org,
            Category = category,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-10)
        };

        // Product B: 10% discount -> discountPercentage = 10.0
        var prodB = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Prod B (10% discount)",
            OriginalPrice = 100m,
            DiscountedPrice = 90m,
            QuantityAvailable = 0,
            ExpirationDate = today.AddDays(5),
            Organization = org,
            Category = category,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-10)
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.AddRange(prodA, prodB);

        // Add discount history for Prod A: Old 100, New 80
        dbContext.PriceHistories.Add(new PriceHistory
        {
            Id = Guid.NewGuid(),
            ProductId = prodA.Id,
            OldOriginalPrice = 100m,
            OldDiscountedPrice = 100m,
            NewOriginalPrice = 100m,
            NewDiscountedPrice = 80m,
            ChangeReason = "AI Assisted Approval",
            ChangedBy = Guid.Empty,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-5)
        });

        // Add discount history for Prod B: Old 100, New 90
        dbContext.PriceHistories.Add(new PriceHistory
        {
            Id = Guid.NewGuid(),
            ProductId = prodB.Id,
            OldOriginalPrice = 100m,
            OldDiscountedPrice = 100m,
            NewOriginalPrice = 100m,
            NewDiscountedPrice = 90m,
            ChangeReason = "AI Assisted Approval",
            ChangedBy = Guid.Empty,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-5)
        });

        await dbContext.SaveChangesAsync();

        HistoricalIngestionRequestDto? capturedRequest = null;
        _mockAiClient.Setup(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<HistoricalIngestionRequestDto, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HistoricalIngestionResponseDto(1, 1, 0, new List<string>()));

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunHistoricalIngestionCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Events.Count.Should().Be(1); // Prod A was skipped due to 20% discount limit, so only Prod B is sent
        capturedRequest.Events[0].ProductId.Should().Be(prodB.Id.ToString());

        var dbEpisodeA = await dbContext.ProductPricingEpisodes.FirstOrDefaultAsync(pe => pe.ProductId == prodA.Id);
        dbEpisodeA.Should().BeNull(); // Skipped product remains eligible for ingestion / sweep retry

        var dbEpisodeB = await dbContext.ProductPricingEpisodes.FirstOrDefaultAsync(pe => pe.ProductId == prodB.Id);
        dbEpisodeB.Should().NotBeNull(); // Valid product has its episode created
    }

    [Fact]
    public async Task Handle_should_not_ingest_or_mark_as_ingested_when_api_throws_contract_exception()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Store A" };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var today = DateOnly.FromDateTime(_mockTimeProvider.Object.GetUtcNow().DateTime);

        var prod = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Product Fail",
            OriginalPrice = 100m,
            DiscountedPrice = 90m,
            QuantityAvailable = 0,
            ExpirationDate = today.AddDays(5),
            Organization = org,
            Category = category,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-10)
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(prod);
        await dbContext.SaveChangesAsync();

        // Simulate API contract violation
        _mockAiClient.Setup(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiServiceContractException("Contract Violation on server response"));

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunHistoricalIngestionCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue(); // Sweep shouldn't crash
        
        var dbEpisode = await dbContext.ProductPricingEpisodes.FirstOrDefaultAsync(pe => pe.ProductId == prod.Id);
        dbEpisode.Should().BeNull(); // Failed product remains eligible for retry
    }

    [Fact]
    public void SalesMetricsCalculator_should_calculate_velocity_relative_to_recorded_at()
    {
        // Arrange
        var productCreatedAt = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var recordedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero); // 9 days old

        var orderItems = new List<SalesMetricsCalculator.OrderItemSummary>
        {
            // Inside last 7 days from recordedAt (Aug 3 to Aug 10)
            new() { Quantity = 5, CreatedAt = new DateTimeOffset(2026, 8, 8, 12, 0, 0, TimeSpan.Zero) },
            new() { Quantity = 2, CreatedAt = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero) },
            // Inside last 30 days but outside 7 days from recordedAt
            new() { Quantity = 10, CreatedAt = new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero) },
            // In the future relative to recordedAt -> should be ignored!
            new() { Quantity = 20, CreatedAt = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero) }
        };

        // Act
        var metrics = SalesMetricsCalculator.Calculate(orderItems, productCreatedAt, recordedAt);

        // Assert
        metrics.SalesVelocity.Should().Be(1.0);
        metrics.HistoricalAverageDailySales.Should().BeApproximately(1.8888, 0.0001);
    }

    [Theory]
    [InlineData(-0.02, true)]  // skip
    [InlineData(-0.005, false)] // clamp to 0
    [InlineData(15.005, false)] // clamp to 15
    [InlineData(15.02, true)]  // skip
    public async Task Handle_should_handle_discount_percentage_boundaries(double rawDiscount, bool shouldSkip)
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Store A" };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var today = DateOnly.FromDateTime(_mockTimeProvider.Object.GetUtcNow().DateTime);

        decimal originalPrice = 100m;
        decimal discountedPrice = originalPrice - (originalPrice * (decimal)rawDiscount / 100m);

        var prod = new Product
        {
            Id = Guid.NewGuid(),
            Title = $"Prod discount {rawDiscount}",
            OriginalPrice = originalPrice,
            DiscountedPrice = discountedPrice,
            QuantityAvailable = 0,
            ExpirationDate = today.AddDays(5),
            Organization = org,
            Category = category,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-10)
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(prod);

        // Add discount history matching prices
        dbContext.PriceHistories.Add(new PriceHistory
        {
            Id = Guid.NewGuid(),
            ProductId = prod.Id,
            OldOriginalPrice = originalPrice,
            OldDiscountedPrice = originalPrice,
            NewOriginalPrice = originalPrice,
            NewDiscountedPrice = discountedPrice,
            ChangeReason = "AI Assisted Approval",
            ChangedBy = Guid.Empty,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-5)
        });

        await dbContext.SaveChangesAsync();

        HistoricalIngestionRequestDto? capturedRequest = null;
        _mockAiClient.Setup(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<HistoricalIngestionRequestDto, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HistoricalIngestionResponseDto(1, 1, 0, new List<string>()));

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunHistoricalIngestionCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        
        var dbEpisode = await dbContext.ProductPricingEpisodes.FirstOrDefaultAsync(pe => pe.ProductId == prod.Id);

        if (shouldSkip)
        {
            capturedRequest.Should().BeNull();
            dbEpisode.Should().BeNull();
        }
        else
        {
            capturedRequest.Should().NotBeNull();
            capturedRequest!.Events.Count.Should().Be(1);
            dbEpisode.Should().NotBeNull();

            var mappedDiscount = capturedRequest.Events[0].DiscountPercentage;
            if (rawDiscount < 0)
            {
                mappedDiscount.Should().Be(0.0);
            }
            else if (rawDiscount > 15)
            {
                mappedDiscount.Should().Be(15.0);
            }
        }
    }

    [Fact]
    public async Task Handle_should_support_multiple_ingested_episodes_per_product_over_lifetime()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Store A" };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var today = DateOnly.FromDateTime(_mockTimeProvider.Object.GetUtcNow().DateTime);

        var prod = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Multi-episode Product",
            OriginalPrice = 100m,
            DiscountedPrice = 90m,
            QuantityAvailable = 0, // finalized
            ExpirationDate = today.AddDays(5),
            Organization = org,
            Category = category,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-20)
        };

        // First discount event: Old 100, New 90
        var disc1 = new PriceHistory
        {
            Id = Guid.NewGuid(),
            ProductId = prod.Id,
            OldOriginalPrice = 100m,
            OldDiscountedPrice = 100m,
            NewOriginalPrice = 100m,
            NewDiscountedPrice = 90m,
            ChangeReason = "AI Assisted Approval",
            ChangedBy = Guid.Empty,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-15)
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(prod);
        dbContext.PriceHistories.Add(disc1);
        await dbContext.SaveChangesAsync();

        HistoricalIngestionRequestDto? capturedRequest1 = null;
        _mockAiClient.Setup(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<HistoricalIngestionRequestDto, CancellationToken>((req, ct) => capturedRequest1 = req)
            .ReturnsAsync(new HistoricalIngestionResponseDto(1, 1, 0, new List<string>()));

        var handler = CreateHandler(dbContext);

        // Run 1: Ingest First Episode
        var result1 = await handler.Handle(new RunHistoricalIngestionCommand(), CancellationToken.None);
        result1.Success.Should().BeTrue();
        capturedRequest1.Should().NotBeNull();
        capturedRequest1!.Events.Count.Should().Be(1);
        capturedRequest1.Events[0].EventId.Should().Be($"ep-{prod.Id}-{disc1.Id}");

        var episodesRun1 = await dbContext.ProductPricingEpisodes.Where(pe => pe.ProductId == prod.Id).ToListAsync();
        episodesRun1.Count.Should().Be(1);
        episodesRun1[0].EventId.Should().Be($"ep-{prod.Id}-{disc1.Id}");

        // Simulate Re-activation and a second discount run!
        // We restock the product, update price, and add a second discount price history
        prod.QuantityAvailable = 0; // Restocked but finalized again later
        var disc2 = new PriceHistory
        {
            Id = Guid.NewGuid(),
            ProductId = prod.Id,
            OldOriginalPrice = 100m,
            OldDiscountedPrice = 90m,
            NewOriginalPrice = 100m,
            NewDiscountedPrice = 85m,
            ChangeReason = "AI Assisted Approval",
            ChangedBy = Guid.Empty,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-2) // fresh event
        };
        dbContext.PriceHistories.Add(disc2);
        await dbContext.SaveChangesAsync();

        HistoricalIngestionRequestDto? capturedRequest2 = null;
        _mockAiClient.Setup(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<HistoricalIngestionRequestDto, CancellationToken>((req, ct) => capturedRequest2 = req)
            .ReturnsAsync(new HistoricalIngestionResponseDto(1, 1, 0, new List<string>()));

        // Run 2: Ingest Second Episode
        var result2 = await handler.Handle(new RunHistoricalIngestionCommand(), CancellationToken.None);
        result2.Success.Should().BeTrue();
        capturedRequest2.Should().NotBeNull();
        capturedRequest2!.Events.Count.Should().Be(1);
        capturedRequest2.Events[0].EventId.Should().Be($"ep-{prod.Id}-{disc2.Id}");

        var episodesRun2 = await dbContext.ProductPricingEpisodes.Where(pe => pe.ProductId == prod.Id).ToListAsync();
        episodesRun2.Count.Should().Be(2); // Ingested both!
        episodesRun2.Any(pe => pe.EventId == $"ep-{prod.Id}-{disc1.Id}").Should().BeTrue();
        episodesRun2.Any(pe => pe.EventId == $"ep-{prod.Id}-{disc2.Id}").Should().BeTrue();
    }

    [Fact]
    public async Task Handle_should_be_idempotent_for_the_same_episode()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Store A" };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var today = DateOnly.FromDateTime(_mockTimeProvider.Object.GetUtcNow().DateTime);

        var prod = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Idempotent Product",
            OriginalPrice = 100m,
            DiscountedPrice = 90m,
            QuantityAvailable = 0,
            ExpirationDate = today.AddDays(5),
            Organization = org,
            Category = category,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-10)
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(prod);
        await dbContext.SaveChangesAsync();

        _mockAiClient.Setup(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HistoricalIngestionResponseDto(1, 1, 0, new List<string>()));

        var handler = CreateHandler(dbContext);

        // Run 1: Sweep processes and ingests product
        var result1 = await handler.Handle(new RunHistoricalIngestionCommand(), CancellationToken.None);
        result1.Success.Should().BeTrue();
        _mockAiClient.Verify(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()), Times.Once);

        var episodesCount = await dbContext.ProductPricingEpisodes.CountAsync(pe => pe.ProductId == prod.Id);
        episodesCount.Should().Be(1);

        // Run 2: Sweep runs again, since no pricing conditions changed, the episode remains the same and is skipped
        _mockAiClient.Invocations.Clear(); // Clear call history
        var result2 = await handler.Handle(new RunHistoricalIngestionCommand(), CancellationToken.None);
        result2.Success.Should().BeTrue();
        _mockAiClient.Verify(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);

        var episodesCountAfter = await dbContext.ProductPricingEpisodes.CountAsync(pe => pe.ProductId == prod.Id);
        episodesCountAfter.Should().Be(1); // Still exactly 1
    }

    [Fact]
    public async Task Database_should_enforce_uniqueness_on_ProductPricingEpisode_EventId_constraint()
    {
        // Arrange
        using var dbContext = CreateSqliteContext();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Store A" };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Uniqueness Test Product",
            OriginalPrice = 100m,
            DiscountedPrice = 90m,
            QuantityAvailable = 0,
            ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            Organization = org,
            Category = category,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var ep1 = new ProductPricingEpisode
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            EventId = "duplicate-event-id",
            RecordedAt = DateTimeOffset.UtcNow,
            IngestedAt = DateTimeOffset.UtcNow,
            IngestionCorrelationId = "corr-1",
            Outcome = "UNSOLD"
        };

        var ep2 = new ProductPricingEpisode
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            EventId = "duplicate-event-id", // duplicate EventId for same ProductId
            RecordedAt = DateTimeOffset.UtcNow,
            IngestedAt = DateTimeOffset.UtcNow,
            IngestionCorrelationId = "corr-2",
            Outcome = "UNSOLD"
        };

        dbContext.ProductPricingEpisodes.Add(ep1);
        await dbContext.SaveChangesAsync();

        // Act
        dbContext.ProductPricingEpisodes.Add(ep2);
        Func<Task> act = async () => await dbContext.SaveChangesAsync();
 
        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public void HistoricalIngestionResponseDto_should_deserialize_correctly_from_literal_json()
    {
        // Arrange
        // accepted_count: number of validation-passed records accepted for processing.
        // upserted_count: number of records successfully written to the vector store.
        // failed_count: number of records rejected or failed during vector store write.
        // document_ids: list of successfully stored document identifiers (matching event IDs).
        // Since accepted and upserted counts may diverge (e.g. accepted-but-not-yet-upserted transient queueing),
        // we track both but map document_ids as the source of truth for completed writes.
        var json = @"{
            ""accepted_count"": 3,
            ""upserted_count"": 2,
            ""failed_count"": 1,
            ""document_ids"": [""doc-1"", ""doc-2""]
        }";

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        // Act
        var response = JsonSerializer.Deserialize<HistoricalIngestionResponseDto>(json, options);

        // Assert
        response.Should().NotBeNull();
        response.AcceptedCount.Should().Be(3);
        response.UpsertedCount.Should().Be(2);
        response.FailedCount.Should().Be(1);
        response.DocumentIds.Should().ContainInOrder("doc-1", "doc-2");
    }

    [Fact]
    public async Task HistoricalIngestion_should_only_stamp_and_persist_episodes_that_did_not_fail()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Store A" };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };

        var today = DateOnly.FromDateTime(_mockTimeProvider.Object.GetUtcNow().DateTime);

        // Product A: Expired (will generate ep-{id}-nodisc)
        var pA = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Product A",
            OriginalPrice = 100m,
            DiscountedPrice = 85m,
            QuantityAvailable = 10,
            ExpirationDate = today.AddDays(-1),
            Organization = org,
            Category = category,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-5)
        };

        // Product B: Expired (will generate ep-{id}-nodisc)
        var pB = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Product B",
            OriginalPrice = 50m,
            DiscountedPrice = 45m,
            QuantityAvailable = 5,
            ExpirationDate = today.AddDays(-1),
            Organization = org,
            Category = category,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-5)
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.AddRange(pA, pB);
        await dbContext.SaveChangesAsync();

        var eventIdA = $"ep-{pA.Id}-nodisc";
        var eventIdB = $"ep-{pB.Id}-nodisc";

        // Mock AI Service to report that only Product A was successfully ingested (upserted/accepted)
        // while Product B failed (it is not in the successful document IDs).
        var mockResponse = new HistoricalIngestionResponseDto(
            AcceptedCount: 1,
            UpsertedCount: 1,
            FailedCount: 1,
            DocumentIds: new List<string> { eventIdA }
        );

        _mockAiClient.Setup(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunHistoricalIngestionCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        // Verify that only the successful episode (Product A) is persisted in the database
        var persistedEpisodes = await dbContext.ProductPricingEpisodes.ToListAsync();
        persistedEpisodes.Should().HaveCount(1);
        persistedEpisodes[0].ProductId.Should().Be(pA.Id);
        persistedEpisodes[0].EventId.Should().Be(eventIdA);
        persistedEpisodes[0].IngestionCorrelationId.Should().Be("test-correlation-id");

        // Verify Product B was not persisted as ingested, leaving it eligible for retry
        persistedEpisodes.Any(pe => pe.ProductId == pB.Id).Should().BeFalse();
    }

    [Fact]
    public void HistoricalIngestionRequestDto_should_serialize_correctly_to_snake_case()
    {
        // Arrange
        var events = new List<HistoricalPricingEventDto>
        {
            new HistoricalPricingEventDto(
                EventId: "ev-1",
                StoreId: "store-1",
                ProductId: "prod-1",
                Category: "Fruit",
                RecordedAt: DateTimeOffset.UtcNow,
                Quantity: 10,
                CurrentPrice: 15.0m,
                OriginalPrice: 20.0m,
                PriceFloor: 12.0m,
                SalesVelocity: 1.5,
                HistoricalAverageDailySales: 3.2, // FLAT field!
                HoursRemaining: 24.0,
                DiscountPercentage: 25.0,
                UnitsSoldAfterDiscount: 4,
                SellThroughRate: 0.4,
                Outcome: "PARTIALLY_SOLD"
            )
        };

        var request = new HistoricalIngestionRequestDto(events);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        // Act
        var json = JsonSerializer.Serialize(request, options);

        // Assert
        // Verify key field mapping from §7.1 matches flat snake_case field name
        json.Should().Contain("\"historical_average_daily_sales\":3.2");
        json.Should().NotContain("demand");
        json.Should().NotContain("historical_sales");
        
        // Check other snake_case fields
        json.Should().Contain("\"event_id\"");
        json.Should().Contain("\"store_id\"");
        json.Should().Contain("\"product_id\"");
        json.Should().Contain("\"units_sold_after_discount\"");
        json.Should().Contain("\"sell_through_rate\"");
        json.Should().Contain("\"hours_remaining\"");
    }

    [Fact]
    public async Task Handle_should_not_call_AiServiceClient_if_all_episodes_are_already_ingested()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Store A" };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var today = DateOnly.FromDateTime(_mockTimeProvider.Object.GetUtcNow().DateTime);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Ingested Product",
            OriginalPrice = 100m,
            DiscountedPrice = 85m,
            QuantityAvailable = 0,
            ExpirationDate = today.AddDays(5),
            Organization = org,
            Category = category,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-10)
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);

        // Add already ingested episode for product
        dbContext.ProductPricingEpisodes.Add(new ProductPricingEpisode
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            EventId = $"ep-{product.Id}-nodisc",
            RecordedAt = product.CreatedAt,
            IngestedAt = DateTimeOffset.UtcNow,
            IngestionCorrelationId = "existing-id",
            Outcome = "UNSOLD"
        });

        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunHistoricalIngestionCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        // Verify client was never called for this episode batch
        _mockAiClient.Verify(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
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
}
