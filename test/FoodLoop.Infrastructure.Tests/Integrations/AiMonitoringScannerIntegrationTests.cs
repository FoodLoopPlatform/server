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
using FoodLoop.Infrastructure.Options;
using FoodLoop.Infrastructure.Tests.TestSupport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Integrations;

public class AiMonitoringScannerIntegrationTests
{
    private readonly Mock<IAiServiceClient> _mockAiClient;
    private readonly Mock<ICorrelationIdAccessor> _mockCorrelationAccessor;
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly MonitoringScannerOptions _options;

    public AiMonitoringScannerIntegrationTests()
    {
        _mockAiClient = new Mock<IAiServiceClient>();
        _mockCorrelationAccessor = new Mock<ICorrelationIdAccessor>();
        _fakeTimeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero));

        _mockCorrelationAccessor.Setup(x => x.GetCorrelationId()).Returns("integration-correlation-id");

        _options = new MonitoringScannerOptions
        {
            IntervalMinutes = 60,
            ExpirationThresholdDays = 3,
            VelocityThresholdMultiplier = 0.8
        };
    }

    private ServiceProvider BuildServiceProvider(IApplicationDbContext dbContext)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(dbContext);
        services.AddSingleton(_mockAiClient.Object);
        services.AddSingleton(_mockCorrelationAccessor.Object);
        services.AddSingleton<TimeProvider>(_fakeTimeProvider);

        var mockIOptions = new Mock<IOptions<MonitoringScannerOptions>>();
        mockIOptions.Setup(x => x.Value).Returns(_options);
        services.AddSingleton(mockIOptions.Object);

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(RunMonitoringScanCommandHandler).Assembly));

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Scan_should_persist_assessments_and_not_mutate_product_prices()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        // 1. Seed database with active store, category and active product
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Organic Store",
            AiOperatingMode = AiOperatingMode.Assisted,
            Latitude = 30.0,
            Longitude = 31.0
        };
        var category = new Category { Id = Guid.NewGuid(), Name = "Organic Produce" };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Apples",
            OriginalPrice = 100.00m,
            DiscountedPrice = 100.00m, // Pre-condition price values
            QuantityAvailable = 50,
            ExpirationDate = DateOnly.FromDateTime(_fakeTimeProvider.GetUtcNow().DateTime).AddDays(1), // Expiry soon
            Status = ProductStatus.Active,
            OrganizationId = org.Id,
            Organization = org,
            CategoryId = category.Id,
            Category = category
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        _mockAiClient.Setup(x => x.AnalyzeMonitoringAsync(It.IsAny<MonitoringRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MonitoringResponseDto("PRICING", "MEDIUM", "Expiry Risk Detected", 0.85));

        var sp = BuildServiceProvider(dbContext);
        var mediator = sp.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new RunMonitoringScanCommand());

        // Assert
        result.Success.Should().BeTrue();

        // 1. Verify AiRiskAssessment record exists in database
        var persistedAssessment = await dbContext.AiRiskAssessments.FirstOrDefaultAsync(a => a.ProductId == product.Id);
        persistedAssessment.Should().NotBeNull();
        persistedAssessment!.RiskLevel.Should().Be(AiRiskLevel.MEDIUM);
        persistedAssessment.Route.Should().Be(AiRoute.PRICING);
        persistedAssessment.IsPricingStaged.Should().BeTrue();
        persistedAssessment.CorrelationId.Should().Be("integration-correlation-id");

        // 2. INVARIANT VERIFICATION: Verify no product prices were mutated (must remain strictly unaltered)
        var freshProduct = await dbContext.Products.FindAsync(product.Id);
        freshProduct.Should().NotBeNull();
        freshProduct!.OriginalPrice.Should().Be(100.00m);
        freshProduct.DiscountedPrice.Should().Be(100.00m);
    }

    [Fact]
    public async Task Scan_should_continue_when_client_throws_exception_on_one_candidate()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), AiOperatingMode = AiOperatingMode.Assisted };
        var category = new Category { Id = Guid.NewGuid(), Name = "Organic Produce" };

        var product1 = new Product
        {
            Id = Guid.NewGuid(),
            Title = "FailedProduct",
            OriginalPrice = 10m,
            DiscountedPrice = 10m,
            QuantityAvailable = 5,
            ExpirationDate = DateOnly.FromDateTime(_fakeTimeProvider.GetUtcNow().DateTime).AddDays(1),
            Status = ProductStatus.Active,
            Organization = org,
            CategoryId = category.Id
        };
        var product2 = new Product
        {
            Id = Guid.NewGuid(),
            Title = "SucceededProduct",
            OriginalPrice = 20m,
            DiscountedPrice = 20m,
            QuantityAvailable = 10,
            ExpirationDate = DateOnly.FromDateTime(_fakeTimeProvider.GetUtcNow().DateTime).AddDays(1),
            Status = ProductStatus.Active,
            Organization = org,
            CategoryId = category.Id
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.AddRange(product1, product2);
        await dbContext.SaveChangesAsync();

        _mockAiClient.Setup(x => x.AnalyzeMonitoringAsync(It.Is<MonitoringRequestDto>(r => r.Product.Name == "FailedProduct"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiServiceUnavailableException("Downstream HTTP failure"));

        _mockAiClient.Setup(x => x.AnalyzeMonitoringAsync(It.Is<MonitoringRequestDto>(r => r.Product.Name == "SucceededProduct"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MonitoringResponseDto("PRICING", "LOW", "Success", 0.9));

        var sp = BuildServiceProvider(dbContext);
        var mediator = sp.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new RunMonitoringScanCommand());

        // Assert
        result.Success.Should().BeTrue(); // Resilience verification

        // SucceededProduct assessment is saved
        var succeedsSaved = await dbContext.AiRiskAssessments.AnyAsync(a => a.ProductId == product2.Id);
        succeedsSaved.Should().BeTrue();

        // FailedProduct assessment is not saved
        var failedSaved = await dbContext.AiRiskAssessments.AnyAsync(a => a.ProductId == product1.Id);
        failedSaved.Should().BeFalse();
    }
}
