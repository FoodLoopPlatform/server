using FluentAssertions;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Features.Organizations.Commands;
using FoodLoop.Application.Features.Organizations.Queries;
using FoodLoop.Application.Features.Products.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Organizations.Commands;
using FoodLoop.Infrastructure.Features.Organizations.Queries;
using FoodLoop.Infrastructure.Features.Products.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Persistence.Repositories;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Organizations;

public class StoreAnalyticsAndDisputeTests
{
    private readonly Mock<IAuditLogService> _mockAudit = new();
    private readonly Mock<ILocalizationService> _mockLoc = new();
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager = MockUserManagerFactory.Create();

    public StoreAnalyticsAndDisputeTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()]).Returns<string>(s => s);
    }

    [Fact]
    public async Task GetStoreAnalytics_ShouldComputeRevenueAndSalesMetrics()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var store = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = "Green Grocery",
            IsDeleted = false
        };
        db.Organizations.Add(store);

        var product1 = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = store.Id,
            Title = "Organic Apples",
            OriginalPrice = 50m,
            DiscountedPrice = 30m,
            QuantityAvailable = 20,
            Status = ProductStatus.Active,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3))
        };
        var product2 = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = store.Id,
            Title = "Sourdough Bread",
            OriginalPrice = 40m,
            DiscountedPrice = 20m,
            QuantityAvailable = 15,
            Status = ProductStatus.Active,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1))
        };
        db.Products.AddRange(product1, product2);

        var customerId = Guid.NewGuid();
        var order1 = new Order
        {
            Id = Guid.NewGuid(),
            UserId = customerId,
            TotalAmount = 60m,
            OrderStatus = OrderStatus.Completed,
            PaymentStatus = PaymentStatus.Paid,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-3),
            Items = new List<OrderItem>
            {
                new OrderItem { ProductId = product1.Id, Quantity = 2, UnitPrice = 30m, Product = product1 }
            }
        };
        var order2 = new Order
        {
            Id = Guid.NewGuid(),
            UserId = customerId,
            TotalAmount = 40m,
            OrderStatus = OrderStatus.Completed,
            PaymentStatus = PaymentStatus.Paid,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            Items = new List<OrderItem>
            {
                new OrderItem { ProductId = product2.Id, Quantity = 2, UnitPrice = 20m, Product = product2 }
            }
        };

        db.Orders.AddRange(order1, order2);
        await db.SaveChangesAsync();

        var handler = new GetStoreAnalyticsQueryHandler(db);

        // Act - Query "all" period
        var resultAll = await handler.Handle(new GetStoreAnalyticsQuery(ownerId, "all"), CancellationToken.None);

        // Assert
        resultAll.Should().NotBeNull();
        resultAll.Revenue.Should().Be(100m); // 60 + 40
        resultAll.CompletedOrdersCount.Should().Be(2);
        resultAll.TopProducts.Should().HaveCount(2);

        // Act - Query "today" period
        var resultToday = await handler.Handle(new GetStoreAnalyticsQuery(ownerId, "today"), CancellationToken.None);
        resultToday.CompletedOrdersCount.Should().Be(2);
        resultToday.Revenue.Should().Be(100m);
        resultToday.AverageOrderValue.Should().Be(50m);
        resultToday.SavingsImpact.Should().Be(80m); // (20*2) + (20*2)
    }

    [Fact]
    public async Task ResolveStoreDispute_ValidDisputeWithRefund_ShouldCreditCustomerWallet()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var merchantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var store = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = merchantId,
            Name = "Super Mart",
            IsDeleted = false
        };
        db.Organizations.Add(store);

        var customer = new ApplicationUser
        {
            Id = customerId,
            UserName = "complaining_customer@test.com",
            WalletBalance = 10m
        };
        db.Users.Add(customer);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = store.Id,
            Title = "Cheese",
            OriginalPrice = 30m,
            DiscountedPrice = 25m,
            Status = ProductStatus.Active,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1))
        };
        db.Products.Add(product);

        var report = new ProductReport
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            ReportedBy = customerId,
            Reason = "Expired on arrival",
            IsResolved = false,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            Product = product
        };
        db.ProductReports.Add(report);
        await db.SaveChangesAsync();

        var handler = new ResolveStoreDisputeCommandHandler(db, _mockAudit.Object);
        var command = new ResolveStoreDisputeCommand(
            DisputeId: report.Id,
            MerchantUserId: merchantId,
            MerchantNote: "Apologies, refunding item cost.",
            RefundAmount: 25m
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsResolved.Should().BeTrue();

        var updatedCustomer = await db.Users.FindAsync(customerId);
        updatedCustomer!.WalletBalance.Should().Be(35m); // 10 + 25

        var walletTx = db.WalletTransactions.FirstOrDefault(w => w.UserId == customerId);
        walletTx.Should().NotBeNull();
        walletTx!.Amount.Should().Be(25m);
        walletTx.Type.Should().Be("Refund");
    }

    [Fact]
    public async Task ResolveStoreDispute_AlreadyResolved_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var merchantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var store = new Organization { Id = Guid.NewGuid(), OwnerId = merchantId, Name = "Store A" };
        var product = new Product { Id = Guid.NewGuid(), OrganizationId = store.Id, Title = "Juice" };
        var report = new ProductReport { Id = Guid.NewGuid(), ProductId = product.Id, ReportedBy = customerId, IsResolved = true, Product = product };

        db.Organizations.Add(store);
        db.Products.Add(product);
        db.ProductReports.Add(report);
        await db.SaveChangesAsync();

        var handler = new ResolveStoreDisputeCommandHandler(db, _mockAudit.Object);

        // Act & Assert
        var act = async () => await handler.Handle(new ResolveStoreDisputeCommand(
            DisputeId: report.Id,
            MerchantUserId: merchantId,
            MerchantNote: "Duplicate resolution",
            RefundAmount: 10m
        ), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetStorePricingOverview_ShouldCalculateDiscountAndPricingMetrics()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var store = new Organization { Id = Guid.NewGuid(), OwnerId = ownerId, Name = "Bakery" };
        db.Organizations.Add(store);

        var p1 = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = store.Id,
            Title = "Cake",
            OriginalPrice = 100m,
            DiscountedPrice = 50m, // 50% discount
            QuantityAvailable = 5,
            Status = ProductStatus.Active,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2))
        };
        var p2 = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = store.Id,
            Title = "Muffin",
            OriginalPrice = 50m,
            DiscountedPrice = 40m, // 20% discount
            QuantityAvailable = 10,
            Status = ProductStatus.Active,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3))
        };
        db.Products.AddRange(p1, p2);
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var handler = new GetStorePricingOverviewQueryHandler(uow);

        // Act
        var result = await handler.Handle(new GetStorePricingOverviewQuery(ownerId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Summary.TotalActiveProducts.Should().Be(2);
        result.Summary.AverageDiscountPercentage.Should().Be(35m); // (50 + 20) / 2
        result.Summary.MaxDiscountPercentage.Should().Be(50m);
        result.Products.Should().HaveCount(2);
    }
}
