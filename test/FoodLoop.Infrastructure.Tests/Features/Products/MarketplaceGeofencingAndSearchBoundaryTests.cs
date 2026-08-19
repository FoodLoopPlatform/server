using FluentAssertions;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Products.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Products.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Products;

public class MarketplaceGeofencingAndSearchBoundaryTests : IDisposable
{
    private readonly ApplicationDbContext _db = ApplicationDbContextFactory.Create();

    private readonly Guid _cairoOrgId = Guid.NewGuid();
    private readonly Guid _gizaOrgId = Guid.NewGuid();
    private readonly Guid _alexOrgId = Guid.NewGuid();
    private readonly Guid _bakeryCatId = Guid.NewGuid();
    private readonly Guid _dairyCatId = Guid.NewGuid();

    public MarketplaceGeofencingAndSearchBoundaryTests()
    {
        var owner = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "owner@markettest.com",
            Email = "owner@markettest.com",
            Status = UserStatus.Active
        };
        _db.Users.Add(owner);

        // Cairo Store: Lat 30.0444, Lon 31.2357 (Downtown Cairo)
        var cairoOrg = new Organization
        {
            Id = _cairoOrgId,
            OwnerId = owner.Id,
            Name = "Cairo Gourmet Bakery",
            Latitude = 30.0444,
            Longitude = 31.2357,
            VerificationStatus = VerificationStatus.Verified
        };

        // Giza Store: Lat 30.0131, Lon 31.2089 (~5 km from Downtown Cairo)
        var gizaOrg = new Organization
        {
            Id = _gizaOrgId,
            OwnerId = owner.Id,
            Name = "Giza Organic Dairy",
            Latitude = 30.0131,
            Longitude = 31.2089,
            VerificationStatus = VerificationStatus.Verified
        };

        // Alexandria Store: Lat 31.2001, Lon 29.9187 (~180 km from Cairo)
        var alexOrg = new Organization
        {
            Id = _alexOrgId,
            OwnerId = owner.Id,
            Name = "Alexandria Fresh Seafood",
            Latitude = 31.2001,
            Longitude = 29.9187,
            VerificationStatus = VerificationStatus.Verified
        };

        _db.Organizations.AddRange(cairoOrg, gizaOrg, alexOrg);

        var bakeryCat = new Category { Id = _bakeryCatId, Name = "Bakery" };
        var dairyCat = new Category { Id = _dairyCatId, Name = "Dairy" };
        _db.Categories.AddRange(bakeryCat, dairyCat);

        // Product 1: Cairo Bakery Bread (10 EGP, 50% discount)
        _db.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = _cairoOrgId,
            CategoryId = _bakeryCatId,
            Title = "Cairo Artisanal Bread",
            Description = "Delicious oven-baked sourdough bread",
            OriginalPrice = 20.0m,
            DiscountedPrice = 10.0m,
            QuantityAvailable = 10,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            Status = ProductStatus.Active
        });

        // Product 2: Giza Fresh Cheese (50 EGP, 20% discount)
        _db.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = _gizaOrgId,
            CategoryId = _dairyCatId,
            Title = "Giza Feta Cheese",
            Description = "Creamy salted white feta cheese",
            OriginalPrice = 60.0m,
            DiscountedPrice = 50.0m,
            QuantityAvailable = 5,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            Status = ProductStatus.Active
        });

        // Product 3: Alexandria Fish (100 EGP)
        _db.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = _alexOrgId,
            CategoryId = _bakeryCatId,
            Title = "Alex Sea Bass Fillet",
            Description = "Fresh catch from Mediterranean sea",
            OriginalPrice = 120.0m,
            DiscountedPrice = 100.0m,
            QuantityAvailable = 4,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            Status = ProductStatus.Active
        });

        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact(DisplayName = "TC-GEO-01: Geofencing radius filter includes near stores and excludes far stores")]
    public async Task GetMarketplaceProducts_GeofencingRadius_FiltersFarStores()
    {
        var handler = new GetMarketplaceProductsQueryHandler(_db);
        // User is in Downtown Cairo (30.0444, 31.2357) looking within 10 km radius
        var query = new GetMarketplaceProductsQuery(
            UserLatitude: 30.0444,
            UserLongitude: 31.2357,
            MaxDistanceKm: 10.0,
            CategoryId: null,
            MinPrice: null,
            MaxPrice: null,
            SearchTerm: null,
            SortBy: null,
            PageNumber: 1,
            PageSize: 20
        );

        var result = await handler.Handle(query, CancellationToken.None);

        // Cairo (~0 km) and Giza (~5 km) must be included; Alexandria (~180 km) must be excluded
        result.Should().HaveCount(2);
        result.Select(p => p.Title).Should().Contain(new[] { "Cairo Artisanal Bread", "Giza Feta Cheese" });
        result.Select(p => p.Title).Should().NotContain("Alex Sea Bass Fillet");
    }

    [Fact(DisplayName = "TC-GEO-02: Sort by distance orders results by proximity")]
    public async Task GetMarketplaceProducts_SortByDistance_ReturnsClosestFirst()
    {
        var handler = new GetMarketplaceProductsQueryHandler(_db);
        // User in Cairo looking with no max distance
        var query = new GetMarketplaceProductsQuery(
            UserLatitude: 30.0444,
            UserLongitude: 31.2357,
            MaxDistanceKm: null,
            CategoryId: null,
            MinPrice: null,
            MaxPrice: null,
            SearchTerm: null,
            SortBy: "distance",
            PageNumber: 1,
            PageSize: 20
        );

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().HaveCount(3);
        result[0].Title.Should().Be("Cairo Artisanal Bread"); // Closest (~0 km)
        result[1].Title.Should().Be("Giza Feta Cheese");        // Second closest (~5 km)
        result[2].Title.Should().Be("Alex Sea Bass Fillet");   // Furthest (~180 km)
    }

    [Fact(DisplayName = "TC-GEO-03: Combined Price Range and Category Filter")]
    public async Task GetMarketplaceProducts_CombinedCategoryAndPriceFilter_MatchesExactly()
    {
        var handler = new GetMarketplaceProductsQueryHandler(_db);
        var query = new GetMarketplaceProductsQuery(
            UserLatitude: null,
            UserLongitude: null,
            MaxDistanceKm: null,
            CategoryId: _bakeryCatId,
            MinPrice: 5.0m,
            MaxPrice: 50.0m, // Excludes Alex fish which is 100m
            SearchTerm: null,
            SortBy: null,
            PageNumber: 1,
            PageSize: 20
        );

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Cairo Artisanal Bread");
    }

    [Fact(DisplayName = "TC-GEO-04: SearchTerm with special characters & case insensitivity matches properly")]
    public async Task GetMarketplaceProducts_SearchTerm_MatchesCaseInsensitively()
    {
        var handler = new GetMarketplaceProductsQueryHandler(_db);
        var query = new GetMarketplaceProductsQuery(
            UserLatitude: null,
            UserLongitude: null,
            MaxDistanceKm: null,
            CategoryId: null,
            MinPrice: null,
            MaxPrice: null,
            SearchTerm: "   sOuRdOuGh   ", // Trimming and case insensitivity
            SortBy: null,
            PageNumber: 1,
            PageSize: 20
        );

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Cairo Artisanal Bread");
    }
}
