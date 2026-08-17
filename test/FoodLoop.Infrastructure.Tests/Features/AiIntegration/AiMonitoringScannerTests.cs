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
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.AiIntegration.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.AiIntegration.Commands;
using FoodLoop.Infrastructure.Options;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.AiIntegration;

public class AiMonitoringScannerTests
{
    private readonly Mock<IAiServiceClient> _mockAiClient;
    private readonly Mock<ICorrelationIdAccessor> _mockCorrelationAccessor;
    private readonly Mock<ILogger<RunMonitoringScanCommandHandler>> _mockLogger;
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly MonitoringScannerOptions _options;

    public AiMonitoringScannerTests()
    {
        _mockAiClient = new Mock<IAiServiceClient>();
        _mockCorrelationAccessor = new Mock<ICorrelationIdAccessor>();
        _mockLogger = new Mock<ILogger<RunMonitoringScanCommandHandler>>();
        _fakeTimeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero));

        _mockCorrelationAccessor.Setup(x => x.GetCorrelationId()).Returns("test-correlation-id");

        _options = new MonitoringScannerOptions
        {
            IntervalMinutes = 60,
            ExpirationThresholdDays = 3,
            VelocityThresholdMultiplier = 0.8
        };
    }

    private RunMonitoringScanCommandHandler CreateHandler(IApplicationDbContext dbContext)
    {
        var mockIOptions = new Mock<IOptions<MonitoringScannerOptions>>();
        mockIOptions.Setup(x => x.Value).Returns(_options);

        return new RunMonitoringScanCommandHandler(
            dbContext,
            _mockAiClient.Object,
            _mockCorrelationAccessor.Object,
            mockIOptions.Object,
            _fakeTimeProvider,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_should_select_and_process_products_nearing_expiry()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Milk",
            OriginalPrice = 10m,
            DiscountedPrice = 9m,
            QuantityAvailable = 5,
            ExpirationDate = DateOnly.FromDateTime(_fakeTimeProvider.GetUtcNow().DateTime).AddDays(2),
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

        _mockAiClient.Setup(x => x.AnalyzeMonitoringAsync(It.IsAny<MonitoringRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MonitoringResponseDto("PRICING", "HIGH", "Nearing Expiry", 0.9));

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunMonitoringScanCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        _mockAiClient.Verify(x => x.AnalyzeMonitoringAsync(It.IsAny<MonitoringRequestDto>(), It.IsAny<CancellationToken>()), Times.Once);

        var assessments = await dbContext.AiRiskAssessments.ToListAsync();
        assessments.Should().ContainSingle();
        var assessment = assessments.First();
        assessment.ProductId.Should().Be(product.Id);
        assessment.Route.Should().Be(AiRoute.PRICING);
        assessment.RiskLevel.Should().Be(AiRiskLevel.HIGH);
        assessment.IsPricingStaged.Should().BeTrue();
        assessment.CorrelationId.Should().Be("test-correlation-id");
    }

    [Fact]
    public async Task Handle_should_select_and_process_products_with_low_velocity()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), AiOperatingMode = AiOperatingMode.Autonomous };
        var category = new Category { Id = Guid.NewGuid(), Name = "Bakery" };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Bread",
            OriginalPrice = 5m,
            DiscountedPrice = 5m,
            QuantityAvailable = 20,
            ExpirationDate = DateOnly.FromDateTime(_fakeTimeProvider.GetUtcNow().DateTime).AddDays(10), // Far expiry
            Status = ProductStatus.Active,
            Organization = org,
            OrganizationId = org.Id,
            Category = category,
            CategoryId = category.Id
        };
        product.CreatedAt = _fakeTimeProvider.GetUtcNow().AddDays(-30);

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        
        var paidOrder = new Order { PaymentStatus = PaymentStatus.Paid };
        var paidOrder2 = new Order { PaymentStatus = PaymentStatus.Paid };
        dbContext.Orders.AddRange(paidOrder, paidOrder2);
        await dbContext.SaveChangesAsync();

        // Under velocity window (last 7 days), product has 1 unit sold (velocity = 0.14)
        // Under historical window (last 30 days), average is 1.0 (qty 30 over 30 days)
        // 0.14 < 1.0 * 0.8 => qualifies
        product.CreatedAt = _fakeTimeProvider.GetUtcNow().AddDays(-30);
        paidOrder.CreatedAt = _fakeTimeProvider.GetUtcNow().AddDays(-5);
        paidOrder2.CreatedAt = _fakeTimeProvider.GetUtcNow().AddDays(-20);
        
        dbContext.Entry(product).Property(p => p.CreatedAt).IsModified = true;
        dbContext.Entry(paidOrder).Property(o => o.CreatedAt).IsModified = true;
        dbContext.Entry(paidOrder2).Property(o => o.CreatedAt).IsModified = true;
        await dbContext.SaveChangesAsync();

        var orderItems = new List<OrderItem>
        {
            new OrderItem { ProductId = product.Id, Quantity = 1, OrderId = paidOrder.Id },
            new OrderItem { ProductId = product.Id, Quantity = 29, OrderId = paidOrder2.Id }
        };
        dbContext.OrderItems.AddRange(orderItems);
        await dbContext.SaveChangesAsync();

        _mockAiClient.Setup(x => x.AnalyzeMonitoringAsync(It.IsAny<MonitoringRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MonitoringResponseDto("PRICING", "MEDIUM", "Low Velocity", 0.75));

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunMonitoringScanCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        _mockAiClient.Verify(x => x.AnalyzeMonitoringAsync(It.IsAny<MonitoringRequestDto>(), It.IsAny<CancellationToken>()), Times.Once);
        
        var assessments = await dbContext.AiRiskAssessments.ToListAsync();
        assessments.Should().ContainSingle();
        assessments.First().Route.Should().Be(AiRoute.PRICING);
        assessments.First().IsPricingStaged.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_should_completely_skip_products_if_mode_is_manual()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), AiOperatingMode = AiOperatingMode.Manual };
        var category = new Category { Id = Guid.NewGuid(), Name = "Snacks" };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Chips",
            OriginalPrice = 3m,
            DiscountedPrice = 3m,
            QuantityAvailable = 10,
            ExpirationDate = DateOnly.FromDateTime(_fakeTimeProvider.GetUtcNow().DateTime).AddDays(1), // Expiry within threshold
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

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunMonitoringScanCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        _mockAiClient.Verify(x => x.AnalyzeMonitoringAsync(It.IsAny<MonitoringRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
        
        var assessments = await dbContext.AiRiskAssessments.ToListAsync();
        assessments.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_should_correctly_route_PRICING_vs_NO_ACTION()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product1 = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Product1",
            ExpirationDate = DateOnly.FromDateTime(_fakeTimeProvider.GetUtcNow().DateTime).AddDays(1),
            Status = ProductStatus.Active,
            Organization = org,
            OrganizationId = org.Id,
            Category = category,
            CategoryId = category.Id
        };
        var product2 = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Product2",
            ExpirationDate = DateOnly.FromDateTime(_fakeTimeProvider.GetUtcNow().DateTime).AddDays(1),
            Status = ProductStatus.Active,
            Organization = org,
            OrganizationId = org.Id,
            Category = category,
            CategoryId = category.Id
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.AddRange(product1, product2);
        await dbContext.SaveChangesAsync();

        _mockAiClient.Setup(x => x.AnalyzeMonitoringAsync(It.Is<MonitoringRequestDto>(d => d.Product.Name == "Product1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MonitoringResponseDto("PRICING", "HIGH", "Action Needed", 0.95));

        _mockAiClient.Setup(x => x.AnalyzeMonitoringAsync(It.Is<MonitoringRequestDto>(d => d.Product.Name == "Product2"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MonitoringResponseDto("NO_ACTION", "LOW", "Fine", 0.99));

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunMonitoringScanCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        
        var assessments = await dbContext.AiRiskAssessments.ToListAsync();
        assessments.Count.Should().Be(2);

        var pricingAssessment = assessments.Single(x => x.ProductId == product1.Id);
        pricingAssessment.Route.Should().Be(AiRoute.PRICING);
        pricingAssessment.IsPricingStaged.Should().BeTrue();

        var noActionAssessment = assessments.Single(x => x.ProductId == product2.Id);
        noActionAssessment.Route.Should().Be(AiRoute.NO_ACTION);
        noActionAssessment.IsPricingStaged.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_should_be_resilient_to_single_candidate_failures()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var product1 = new Product
        {
            Id = Guid.NewGuid(),
            Title = "FailedProduct",
            ExpirationDate = DateOnly.FromDateTime(_fakeTimeProvider.GetUtcNow().DateTime).AddDays(1),
            Status = ProductStatus.Active,
            Organization = org,
            OrganizationId = org.Id,
            Category = category,
            CategoryId = category.Id
        };
        var product2 = new Product
        {
            Id = Guid.NewGuid(),
            Title = "SuccessfulProduct",
            ExpirationDate = DateOnly.FromDateTime(_fakeTimeProvider.GetUtcNow().DateTime).AddDays(1),
            Status = ProductStatus.Active,
            Organization = org,
            OrganizationId = org.Id,
            Category = category,
            CategoryId = category.Id
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.AddRange(product1, product2);
        await dbContext.SaveChangesAsync();

        // Set first to throw exception
        _mockAiClient.Setup(x => x.AnalyzeMonitoringAsync(It.Is<MonitoringRequestDto>(d => d.Product.Name == "FailedProduct"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiServiceUnavailableException("AI Service is offline"));

        // Set second to succeed
        _mockAiClient.Setup(x => x.AnalyzeMonitoringAsync(It.Is<MonitoringRequestDto>(d => d.Product.Name == "SuccessfulProduct"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MonitoringResponseDto("PRICING", "HIGH", "Fine", 0.9));

        var handler = CreateHandler(dbContext);

        // Act
        var result = await handler.Handle(new RunMonitoringScanCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue(); // Should not fail overall scan
        
        var assessments = await dbContext.AiRiskAssessments.ToListAsync();
        assessments.Should().ContainSingle();
        assessments.First().ProductId.Should().Be(product2.Id);
    }
}
