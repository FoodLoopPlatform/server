using FluentAssertions;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Admin.Commands;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Admin;

public class ExtendProductExpirationTests
{
    [Fact]
    public async Task ExtendProductExpiration_AllProducts_ShouldExtendDatesAndReactivateExpired()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var store = new Organization { Id = Guid.NewGuid(), Name = "Bakery 1" };
        var category = new Category { Id = Guid.NewGuid(), Name = "Pastry" };
        db.Organizations.Add(store);
        db.Categories.Add(category);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Product 1: Expired yesterday
        var prod1 = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Croissant",
            OrganizationId = store.Id,
            CategoryId = category.Id,
            OriginalPrice = 50m,
            DiscountedPrice = 50m,
            ExpirationDate = today.AddDays(-1),
            Status = ProductStatus.Expired
        };

        // Product 2: Expires in 2 days
        var prod2 = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Baguette",
            OrganizationId = store.Id,
            CategoryId = category.Id,
            OriginalPrice = 30m,
            DiscountedPrice = 30m,
            ExpirationDate = today.AddDays(2),
            Status = ProductStatus.Active
        };

        db.Products.AddRange(prod1, prod2);
        await db.SaveChangesAsync();

        var handler = new ExtendProductExpirationCommandHandler(db, NullLogger<ExtendProductExpirationCommandHandler>.Instance);

        // Act - Extend by 10 days
        var command = new ExtendProductExpirationCommand(Days: 10, ReactivateExpiredProducts: true);
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalProductsUpdated.Should().Be(2);
        result.Data.ReactivatedCount.Should().Be(1);

        var updatedProd1 = await db.Products.FindAsync(prod1.Id);
        updatedProd1!.ExpirationDate.Should().Be(today.AddDays(10));
        updatedProd1.Status.Should().Be(ProductStatus.Active);

        var updatedProd2 = await db.Products.FindAsync(prod2.Id);
        updatedProd2!.ExpirationDate.Should().Be(today.AddDays(12));
        updatedProd2.Status.Should().Be(ProductStatus.Active);
    }

    [Fact]
    public async Task ExtendProductExpiration_FilteredByStoreId_ShouldOnlyExtendProductsForThatStore()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var storeA = new Organization { Id = Guid.NewGuid(), Name = "Store A" };
        var storeB = new Organization { Id = Guid.NewGuid(), Name = "Store B" };
        var category = new Category { Id = Guid.NewGuid(), Name = "General" };

        db.Organizations.AddRange(storeA, storeB);
        db.Categories.Add(category);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var prodA = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Product A",
            OrganizationId = storeA.Id,
            CategoryId = category.Id,
            OriginalPrice = 10m,
            DiscountedPrice = 10m,
            ExpirationDate = today.AddDays(1),
            Status = ProductStatus.Active
        };

        var prodB = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Product B",
            OrganizationId = storeB.Id,
            CategoryId = category.Id,
            OriginalPrice = 20m,
            DiscountedPrice = 20m,
            ExpirationDate = today.AddDays(1),
            Status = ProductStatus.Active
        };

        db.Products.AddRange(prodA, prodB);
        await db.SaveChangesAsync();

        var handler = new ExtendProductExpirationCommandHandler(db, NullLogger<ExtendProductExpirationCommandHandler>.Instance);

        // Act - Extend only Store A by 5 days
        var command = new ExtendProductExpirationCommand(Days: 5, ReactivateExpiredProducts: true, StoreId: storeA.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.TotalProductsUpdated.Should().Be(1);

        var updatedProdA = await db.Products.FindAsync(prodA.Id);
        updatedProdA!.ExpirationDate.Should().Be(today.AddDays(6));

        var untouchedProdB = await db.Products.FindAsync(prodB.Id);
        untouchedProdB!.ExpirationDate.Should().Be(today.AddDays(1)); // Untouched
    }

    [Fact]
    public async Task ExtendProductExpiration_NoProducts_ShouldReturnZeroCountGracefully()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var handler = new ExtendProductExpirationCommandHandler(db, NullLogger<ExtendProductExpirationCommandHandler>.Instance);

        // Act
        var result = await handler.Handle(new ExtendProductExpirationCommand(Days: 7), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalProductsUpdated.Should().Be(0);
        result.Data.Message.Should().Contain("No eligible products found");
    }
}
