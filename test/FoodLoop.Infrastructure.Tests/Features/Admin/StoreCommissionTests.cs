using FluentAssertions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Admin.Commands;
using FoodLoop.Infrastructure.Features.Admin.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Admin;

public class StoreCommissionTests : IDisposable
{
    private readonly ApplicationDbContext _db = ApplicationDbContextFactory.Create();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IAuditLogService> _auditLog = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetStoreCommissions_should_return_accurate_sales_and_commission()
    {
        // Arrange
        var merchantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        // 1. Seed global settings
        var settings = new SystemSettings { Id = SystemSettings.SingletonId, PlatformCommissionPercent = 10 };
        _db.SystemSettings.Add(settings);

        // 2. Seed merchant user and store
        var merchant = new ApplicationUser { Id = merchantId, Email = "merchant@store.com", UserName = "merchant@store.com" };
        _db.Users.Add(merchant);

        var store = new Organization
        {
            Id = storeId,
            OwnerId = merchantId,
            Name = "Tasty Store",
            CommissionWithdrawn = 0.00m
        };
        _db.Organizations.Add(store);

        // 3. Seed product
        var productId = Guid.NewGuid();
        var product = new Product
        {
            Id = productId,
            OrganizationId = storeId,
            Title = "Discounted Meal",
            OriginalPrice = 100.00m,
            DiscountedPrice = 50.00m
        };
        _db.Products.Add(product);

        // 4. Seed a completed order (Sales = 100.00)
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            UserId = Guid.NewGuid(),
            OrderStatus = OrderStatus.Completed,
            PaymentStatus = PaymentStatus.Paid,
            TotalAmount = 100.00m
        };
        _db.Orders.Add(order);

        var orderItem = new OrderItem
        {
            OrderId = orderId,
            ProductId = productId,
            Quantity = 2,
            UnitPrice = 50.00m
        };
        _db.OrderItems.Add(orderItem);

        await _db.SaveChangesAsync();

        var queryHandler = new GetStoreCommissionsQueryHandler(_db);
        var query = new GetStoreCommissionsQuery();

        // Act
        var result = await queryHandler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result.First();
        dto.StoreId.Should().Be(storeId);
        dto.StoreName.Should().Be("Tasty Store");
        dto.OwnerEmail.Should().Be("merchant@store.com");
        dto.PlatformCommissionPercent.Should().Be(10);
        dto.TotalSales.Should().Be(100.00m);
        dto.TotalCommissionGenerated.Should().Be(10.00m);
        dto.CommissionWithdrawn.Should().Be(0.00m);
        dto.OutstandingCommission.Should().Be(10.00m);
    }

    [Fact]
    public async Task WithdrawCommission_should_succeed_and_update_withdrawn_balance()
    {
        // Arrange
        var merchantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var adminGuid = Guid.NewGuid();
        _currentUser.Setup(u => u.UserId).Returns(adminGuid);

        var settings = new SystemSettings { Id = SystemSettings.SingletonId, PlatformCommissionPercent = 10 };
        _db.SystemSettings.Add(settings);

        var merchant = new ApplicationUser { Id = merchantId, Email = "merchant@store.com", UserName = "merchant@store.com" };
        _db.Users.Add(merchant);

        var store = new Organization
        {
            Id = storeId,
            OwnerId = merchantId,
            Name = "Tasty Store",
            CommissionWithdrawn = 2.00m // already withdrew 2.00
        };
        _db.Organizations.Add(store);

        var productId = Guid.NewGuid();
        var product = new Product { Id = productId, OrganizationId = storeId, Title = "Meal" };
        _db.Products.Add(product);

        var orderId = Guid.NewGuid();
        var order = new Order { Id = orderId, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid };
        _db.Orders.Add(order);

        var orderItem = new OrderItem { OrderId = orderId, ProductId = productId, Quantity = 2, UnitPrice = 50.00m }; // Total Sales = 100.00, Comm = 10.00, Outstanding = 8.00
        _db.OrderItems.Add(orderItem);

        await _db.SaveChangesAsync();

        var handler = new WithdrawStoreCommissionCommandHandler(_db, _currentUser.Object, _auditLog.Object);
        var command = new WithdrawStoreCommissionCommand(storeId, 5.00m);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.CommissionWithdrawn.Should().Be(7.00m);
        result.OutstandingCommission.Should().Be(3.00m);

        var updatedStore = await _db.Organizations.FindAsync(storeId);
        updatedStore!.CommissionWithdrawn.Should().Be(7.00m);

        _auditLog.Verify(l => l.LogAsync(
            adminGuid,
            storeId,
            "CommissionWithdrawn",
            "Commission Withdrawn",
            It.Is<string>(s => s.Contains("5.00")),
            null,
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task WithdrawCommission_should_throw_if_amount_exceeds_outstanding()
    {
        // Arrange
        var storeId = Guid.NewGuid();
        var settings = new SystemSettings { Id = SystemSettings.SingletonId, PlatformCommissionPercent = 10 };
        _db.SystemSettings.Add(settings);

        var store = new Organization { Id = storeId, OwnerId = Guid.NewGuid(), Name = "Tasty Store", CommissionWithdrawn = 0.00m };
        _db.Organizations.Add(store);

        await _db.SaveChangesAsync();

        var handler = new WithdrawStoreCommissionCommandHandler(_db, _currentUser.Object, _auditLog.Object);
        var command = new WithdrawStoreCommissionCommand(storeId, 10.00m); // exceeds outstanding of 0.00

        // Act & Assert
        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*exceeds the outstanding commission*");
    }

    [Fact]
    public async Task WithdrawCommission_should_throw_if_amount_is_zero_or_negative()
    {
        // Arrange
        var storeId = Guid.NewGuid();
        var store = new Organization { Id = storeId, OwnerId = Guid.NewGuid(), Name = "Tasty Store", CommissionWithdrawn = 0.00m };
        _db.Organizations.Add(store);

        await _db.SaveChangesAsync();

        var handler = new WithdrawStoreCommissionCommandHandler(_db, _currentUser.Object, _auditLog.Object);
        var command = new WithdrawStoreCommissionCommand(storeId, -1.00m);

        // Act & Assert
        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*greater than zero*");
    }
}
