using FluentAssertions;
using FoodLoop.Application.Features.Products.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Products.Queries;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Products;

public class MarketplaceQueryHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext = ApplicationDbContextFactory.Create();
    private readonly Guid _verifiedOrgId = Guid.NewGuid();
    private readonly Guid _unverifiedOrgId = Guid.NewGuid();
    private readonly Guid _categoryId1 = Guid.NewGuid();
    private readonly Guid _categoryId2 = Guid.NewGuid();

    public MarketplaceQueryHandlerTests()
    {
        // Seed database
        var category1 = new Category { Id = _categoryId1, Name = "Bakery" };
        var category2 = new Category { Id = _categoryId2, Name = "Produce" };
        _dbContext.Categories.AddRange(category1, category2);

        // Cairo Coordinates (e.g., 30.0444, 31.2357)
        var verifiedOrg = new Organization
        {
            Id = _verifiedOrgId,
            OwnerId = Guid.NewGuid(),
            Name = "Verified Store",
            VerificationStatus = VerificationStatus.Verified,
            Latitude = 30.0444,
            Longitude = 31.2357
        };

        var unverifiedOrg = new Organization
        {
            Id = _unverifiedOrgId,
            OwnerId = Guid.NewGuid(),
            Name = "Unverified Store",
            VerificationStatus = VerificationStatus.Unverified,
            Latitude = 30.0444,
            Longitude = 31.2357
        };

        _dbContext.Organizations.AddRange(verifiedOrg, unverifiedOrg);

        // Products for Verified Store (Active, in-stock, unexpired)
        var product1 = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = _verifiedOrgId,
            CategoryId = _categoryId1,
            Title = "Croissant",
            Description = "Delicious french pastry",
            OriginalPrice = 10.0m,
            DiscountedPrice = 5.0m,
            QuantityAvailable = 5,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            Status = ProductStatus.Active
        };

        var product2 = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = _verifiedOrgId,
            CategoryId = _categoryId2,
            Title = "Apples",
            Description = "Fresh red apples",
            OriginalPrice = 12.0m,
            DiscountedPrice = 8.0m,
            QuantityAvailable = 10,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
            Status = ProductStatus.Active
        };

        // Product from Unverified Store (Should not show)
        var productUnverified = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = _unverifiedOrgId,
            CategoryId = _categoryId1,
            Title = "Bread",
            OriginalPrice = 5.0m,
            DiscountedPrice = 2.0m,
            QuantityAvailable = 8,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            Status = ProductStatus.Active
        };

        // Expired Product (Should not show)
        var productExpired = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = _verifiedOrgId,
            CategoryId = _categoryId1,
            Title = "Old Pastry",
            OriginalPrice = 10.0m,
            DiscountedPrice = 1.0m,
            QuantityAvailable = 2,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
            Status = ProductStatus.Active
        };

        // Out of Stock Product (Should not show)
        var productOutOfStock = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = _verifiedOrgId,
            CategoryId = _categoryId1,
            Title = "Sold Out Donut",
            OriginalPrice = 10.0m,
            DiscountedPrice = 4.0m,
            QuantityAvailable = 0,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            Status = ProductStatus.Active
        };

        // Non-Active Product (Should not show)
        var productPending = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = _verifiedOrgId,
            CategoryId = _categoryId1,
            Title = "Pending Cupcake",
            OriginalPrice = 8.0m,
            DiscountedPrice = 4.0m,
            QuantityAvailable = 15,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(4)),
            Status = ProductStatus.PendingModeration
        };

        _dbContext.Products.AddRange(product1, product2, productUnverified, productExpired, productOutOfStock, productPending);
        _dbContext.SaveChanges();
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_should_only_return_active_unexpired_instock_products_from_verified_stores()
    {
        // Arrange
        var handler = new GetMarketplaceProductsQueryHandler(_dbContext);
        var query = new GetMarketplaceProductsQuery(null, null, null, null, null, null, null, null, 1, 10);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Select(p => p.Title).Should().Contain(new[] { "Croissant", "Apples" });
        result.Select(p => p.Title).Should().NotContain(new[] { "Bread", "Old Pastry", "Sold Out Donut", "Pending Cupcake" });
    }

    [Fact]
    public async Task Handle_should_filter_by_search_term()
    {
        // Arrange
        var handler = new GetMarketplaceProductsQueryHandler(_dbContext);
        var query = new GetMarketplaceProductsQuery(null, null, null, null, null, null, "croiss", null, 1, 10);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Croissant");
    }

    [Fact]
    public async Task Handle_should_filter_by_category()
    {
        // Arrange
        var handler = new GetMarketplaceProductsQueryHandler(_dbContext);
        var query = new GetMarketplaceProductsQuery(null, null, null, _categoryId2, null, null, null, null, 1, 10);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Apples");
    }

    [Fact]
    public async Task Handle_should_filter_by_price_range()
    {
        // Arrange
        var handler = new GetMarketplaceProductsQueryHandler(_dbContext);
        var query = new GetMarketplaceProductsQuery(null, null, null, null, 6.0m, 9.0m, null, null, 1, 10);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Apples");
    }

    [Fact]
    public async Task Handle_should_calculate_distance_and_sort_correctly()
    {
        // Arrange
        var handler = new GetMarketplaceProductsQueryHandler(_dbContext);
        
        // Cairo Tower (coordinates: 30.0459, 31.2243)
        // Verified Store is at (30.0444, 31.2357). Distance is ~1.1km.
        var query = new GetMarketplaceProductsQuery(30.0459, 31.2243, 5.0, null, null, null, null, "distance", 1, 10);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.All(p => p.DistanceKm.HasValue).Should().BeTrue();
        result.First().DistanceKm.Value.Should().BeInRange(0.5, 2.0);
    }
}
