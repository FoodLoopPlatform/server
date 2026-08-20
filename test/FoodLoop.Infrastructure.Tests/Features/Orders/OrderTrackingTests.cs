using FluentAssertions;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Features.Orders.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Orders.Queries;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Tests.TestSupport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Orders;

public class OrderTrackingTests
{
    [Fact]
    public async Task GetOrderTracking_ValidCustomer_ShouldReturnPipelineStepsAndOrderDetails()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var customerId = Guid.NewGuid();
        var storeOwnerId = Guid.NewGuid();

        var store = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = storeOwnerId,
            Name = "Fresh Market",
            Logo = "https://example.com/logo.png",
            IsDeleted = false
        };
        db.Organizations.Add(store);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = store.Id,
            Title = "Fresh Milk",
            OriginalPrice = 30m,
            DiscountedPrice = 20m,
            QuantityAvailable = 10,
            Status = ProductStatus.Active,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            Organization = store
        };
        db.Products.Add(product);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = customerId,
            TotalAmount = 40m,
            OrderStatus = OrderStatus.Preparing,
            PaymentStatus = PaymentStatus.Paid,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            Items = new List<OrderItem>
            {
                new OrderItem { ProductId = product.Id, Quantity = 2, UnitPrice = 20m, Product = product }
            }
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new GetOrderTrackingQueryHandler(db);

        // Act
        var result = await handler.Handle(new GetOrderTrackingQuery(order.Id, customerId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().Be(order.Id);
        result.OrderStatus.Should().Be("Preparing");
        result.StoreName.Should().Be("Fresh Market");
        result.StoreLogo.Should().Be("https://example.com/logo.png");
        result.TotalAmount.Should().Be(40m);
        result.Items.Should().HaveCount(1);
        result.Items.First().ProductTitle.Should().Be("Fresh Milk");

        // Check pipeline steps
        result.Steps.Should().HaveCount(5); // Pending, Confirmed, Preparing, ReadyForPickup, Completed
        var preparingStep = result.Steps.First(s => s.Status == "Preparing");
        preparingStep.Completed.Should().BeTrue();

        var pendingStep = result.Steps.First(s => s.Status == "Pending");
        pendingStep.Completed.Should().BeTrue();

        var readyStep = result.Steps.First(s => s.Status == "ReadyForPickup");
        readyStep.Completed.Should().BeFalse();
    }

    [Fact]
    public async Task GetOrderTracking_MerchantOfOrder_ShouldBeAllowedAccess()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var customerId = Guid.NewGuid();
        var storeOwnerId = Guid.NewGuid();

        var store = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = storeOwnerId,
            Name = "Bakery Mart",
            IsDeleted = false
        };
        db.Organizations.Add(store);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = store.Id,
            Title = "Baguette",
            OriginalPrice = 15m,
            DiscountedPrice = 10m,
            QuantityAvailable = 5,
            Status = ProductStatus.Active,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            Organization = store
        };
        db.Products.Add(product);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = customerId,
            TotalAmount = 10m,
            OrderStatus = OrderStatus.ReadyForPickup,
            PaymentStatus = PaymentStatus.Paid,
            Items = new List<OrderItem>
            {
                new OrderItem { ProductId = product.Id, Quantity = 1, UnitPrice = 10m, Product = product }
            }
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new GetOrderTrackingQueryHandler(db);

        // Act - Queried by the store owner
        var result = await handler.Handle(new GetOrderTrackingQuery(order.Id, storeOwnerId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.OrderStatus.Should().Be("ReadyForPickup");
    }

    [Fact]
    public async Task GetOrderTracking_UnauthorizedUser_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var customerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var storeOwnerId = Guid.NewGuid();

        var store = new Organization { Id = Guid.NewGuid(), OwnerId = storeOwnerId, Name = "Store" };
        var product = new Product { Id = Guid.NewGuid(), OrganizationId = store.Id, Title = "P1", Organization = store };
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = customerId,
            TotalAmount = 10m,
            OrderStatus = OrderStatus.Confirmed,
            Items = new List<OrderItem> { new OrderItem { ProductId = product.Id, Quantity = 1, UnitPrice = 10m, Product = product } }
        };

        db.Organizations.Add(store);
        db.Products.Add(product);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new GetOrderTrackingQueryHandler(db);

        // Act & Assert
        var act = async () => await handler.Handle(new GetOrderTrackingQuery(order.Id, strangerId), CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
