using FluentAssertions;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Orders;
using FoodLoop.Application.Features.Orders.Commands;
using FoodLoop.Application.Features.Orders.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Orders.Commands;
using FoodLoop.Infrastructure.Features.Orders.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Orders;

public class OrderCommandHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext = ApplicationDbContextFactory.Create();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _merchantId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _productId1 = Guid.NewGuid();
    private readonly Guid _productId2 = Guid.NewGuid();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly Mock<IRealTimeNotificationService> _notification = new();

    public OrderCommandHandlerTests()
    {
        // Seed initial data
        var customer = new ApplicationUser
        {
            Id = _customerId,
            UserName = "customer@example.com",
            Email = "customer@example.com",
            FullName = "Customer Test",
            Status = UserStatus.Active
        };

        var merchant = new ApplicationUser
        {
            Id = _merchantId,
            UserName = "merchant@example.com",
            Email = "merchant@example.com",
            FullName = "Merchant Test",
            Status = UserStatus.Active
        };

        _dbContext.Users.AddRange(customer, merchant);

        var organization = new Organization
        {
            Id = _organizationId,
            OwnerId = _merchantId,
            Name = "Test Bakery",
            VerificationStatus = VerificationStatus.Verified
        };
        _dbContext.Organizations.Add(organization);

        var category = new Category { Id = Guid.NewGuid(), Name = "Bakery" };
        _dbContext.Categories.Add(category);

        var product1 = new Product
        {
            Id = _productId1,
            OrganizationId = _organizationId,
            CategoryId = category.Id,
            Title = "Cake Slice",
            OriginalPrice = 10.0m,
            DiscountedPrice = 6.0m,
            QuantityAvailable = 5,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            Status = ProductStatus.Active
        };

        var product2 = new Product
        {
            Id = _productId2,
            OrganizationId = _organizationId,
            CategoryId = category.Id,
            Title = "Bagel",
            OriginalPrice = 5.0m,
            DiscountedPrice = 3.0m,
            QuantityAvailable = 10,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            Status = ProductStatus.Active
        };

        _dbContext.Products.AddRange(product1, product2);
        _dbContext.SaveChanges();
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task CreateOrder_should_deduct_inventory_and_succeed_with_paid_payment()
    {
        // Arrange
        var handler = new CreateOrderCommandHandler(_dbContext, _auditLogService.Object, _notification.Object);
        var command = new CreateOrderCommand(
            UserId: _customerId,
            Items: new List<CheckoutItemRequest>
            {
                new(_productId1, 2),
                new(_productId2, 3)
            },
            IpAddress: "127.0.0.1"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalAmount.Should().Be(2 * 6.0m + 3 * 3.0m); // 21.00
        result.Data.OrderStatus.Should().Be("Pending");
        result.Data.PaymentStatus.Should().Be("Paid"); // simulated paid status
        result.Data.Items.Should().HaveCount(2);

        // Verify inventory deduction
        var p1 = await _dbContext.Products.FindAsync(_productId1);
        p1!.QuantityAvailable.Should().Be(3);

        var p2 = await _dbContext.Products.FindAsync(_productId2);
        p2!.QuantityAvailable.Should().Be(7);

        // Verify audit logs were written
        _auditLogService.Verify(a => a.LogAsync(
            _customerId,
            null,
            "OrderPlaced",
            "Order Placed",
            It.IsAny<string>(),
            "127.0.0.1",
            It.IsAny<CancellationToken>()), Times.Once);

        _auditLogService.Verify(a => a.LogAsync(
            null,
            _organizationId,
            "OrderReceived",
            "Order Received",
            It.IsAny<string>(),
            "127.0.0.1",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_should_fail_when_insufficient_stock()
    {
        // Arrange
        var handler = new CreateOrderCommandHandler(_dbContext, _auditLogService.Object, _notification.Object);
        var command = new CreateOrderCommand(
            UserId: _customerId,
            Items: new List<CheckoutItemRequest>
            {
                new(_productId1, 6) // request exceeds available stock (5)
            },
            IpAddress: "127.0.0.1"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Insufficient stock");
    }

    [Fact]
    public async Task UpdateOrderStatus_to_Cancelled_should_restore_stock_successfully()
    {
        // Arrange
        var setupHandler = new CreateOrderCommandHandler(_dbContext, _auditLogService.Object, _notification.Object);
        var setupCommand = new CreateOrderCommand(
            UserId: _customerId,
            Items: new List<CheckoutItemRequest> { new(_productId1, 2) },
            IpAddress: "127.0.0.1"
        );
        var setupResult = await setupHandler.Handle(setupCommand, CancellationToken.None);
        var orderId = setupResult.Data!.Id;

        var handler = new UpdateOrderStatusCommandHandler(_dbContext, _auditLogService.Object, _notification.Object);
        var command = new UpdateOrderStatusCommand(_merchantId, orderId, "Cancelled");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.OrderStatus.Should().Be("Cancelled");
        result.Data.PaymentStatus.Should().Be("Refunded");

        // Verify stock is restored (original 5 - 2 + 2 = 5)
        var p1 = await _dbContext.Products.FindAsync(_productId1);
        p1!.QuantityAvailable.Should().Be(5);
    }

    [Fact]
    public async Task GetOrderDetail_should_reject_unauthorized_user()
    {
        // Arrange
        var setupHandler = new CreateOrderCommandHandler(_dbContext, _auditLogService.Object, _notification.Object);
        var setupCommand = new CreateOrderCommand(
            UserId: _customerId,
            Items: new List<CheckoutItemRequest> { new(_productId1, 2) },
            IpAddress: "127.0.0.1"
        );
        var setupResult = await setupHandler.Handle(setupCommand, CancellationToken.None);
        var orderId = setupResult.Data!.Id;

        var handler = new GetOrderDetailQueryHandler(_dbContext);
        var strangerId = Guid.NewGuid();
        var query = new GetOrderDetailQuery(orderId, strangerId);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(query, CancellationToken.None));
    }
}
