using FluentAssertions;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Orders;
using FoodLoop.Application.Features.Orders.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Orders.Commands;
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

public class OrderStateMachineAndConcurrencyTests : IDisposable
{
    private readonly ApplicationDbContext _db = ApplicationDbContextFactory.Create();
    private readonly Mock<IAuditLogService> _mockAuditLog = new();
    private readonly Mock<IRealTimeNotificationService> _mockNotification = new();

    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _merchantId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();

    public OrderStateMachineAndConcurrencyTests()
    {
        var customer = new ApplicationUser
        {
            Id = _customerId,
            UserName = "customer@foodloop.test",
            Email = "customer@foodloop.test",
            FullName = "Test Customer",
            Status = UserStatus.Active
        };

        var merchant = new ApplicationUser
        {
            Id = _merchantId,
            UserName = "merchant@foodloop.test",
            Email = "merchant@foodloop.test",
            FullName = "Test Merchant",
            Status = UserStatus.Active
        };

        _db.Users.AddRange(customer, merchant);

        var org = new Organization
        {
            Id = _organizationId,
            OwnerId = _merchantId,
            Name = "Fresh Market Org",
            VerificationStatus = VerificationStatus.Verified
        };
        _db.Organizations.Add(org);

        var cat = new Category
        {
            Id = _categoryId,
            Name = "Produce"
        };
        _db.Categories.Add(cat);

        var product = new Product
        {
            Id = _productId,
            OrganizationId = _organizationId,
            CategoryId = _categoryId,
            Title = "Organic Strawberries 500g",
            OriginalPrice = 50.0m,
            DiscountedPrice = 30.0m,
            QuantityAvailable = 10,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            Status = ProductStatus.Active
        };
        _db.Products.Add(product);

        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact(DisplayName = "TC-ORD-01: Empty cart creation returns failure")]
    public async Task CreateOrder_EmptyCart_ShouldReturnFail()
    {
        var handler = new CreateOrderCommandHandler(_db, _mockAuditLog.Object, _mockNotification.Object);
        var command = new CreateOrderCommand(_customerId, new List<CheckoutItemRequest>(), "127.0.0.1");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Cannot checkout an empty cart");
    }

    [Fact(DisplayName = "TC-ORD-02: Suspended customer placing order returns failure")]
    public async Task CreateOrder_SuspendedCustomer_ShouldReturnFail()
    {
        var suspendedCustomer = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "suspended@foodloop.test",
            FullName = "Suspended User",
            Status = UserStatus.Suspended
        };
        _db.Users.Add(suspendedCustomer);
        await _db.SaveChangesAsync();

        var handler = new CreateOrderCommandHandler(_db, _mockAuditLog.Object, _mockNotification.Object);
        var command = new CreateOrderCommand(
            suspendedCustomer.Id,
            new List<CheckoutItemRequest> { new(_productId, 1) },
            "127.0.0.1");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("account must be active");
    }

    [Fact(DisplayName = "TC-ORD-03: Non-existent product in cart returns failure")]
    public async Task CreateOrder_NonExistentProduct_ShouldReturnFail()
    {
        var missingId = Guid.NewGuid();
        var handler = new CreateOrderCommandHandler(_db, _mockAuditLog.Object, _mockNotification.Object);
        var command = new CreateOrderCommand(
            _customerId,
            new List<CheckoutItemRequest> { new(missingId, 1) },
            "127.0.0.1");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain($"Product with ID {missingId} not found");
    }

    [Fact(DisplayName = "TC-ORD-04: Expired product in cart fails checkout")]
    public async Task CreateOrder_ExpiredProduct_ShouldReturnFail()
    {
        var expiredProd = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CategoryId = _categoryId,
            Title = "Expired Milk",
            OriginalPrice = 20m,
            DiscountedPrice = 10m,
            QuantityAvailable = 5,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), // Expired yesterday
            Status = ProductStatus.Active
        };
        _db.Products.Add(expiredProd);
        await _db.SaveChangesAsync();

        var handler = new CreateOrderCommandHandler(_db, _mockAuditLog.Object, _mockNotification.Object);
        var command = new CreateOrderCommand(
            _customerId,
            new List<CheckoutItemRequest> { new(expiredProd.Id, 1) },
            "127.0.0.1");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("no longer active or has expired");
    }

    [Fact(DisplayName = "TC-ORD-05: Product with PendingModeration status fails checkout")]
    public async Task CreateOrder_PendingModerationProduct_ShouldReturnFail()
    {
        var unverifiedProd = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CategoryId = _categoryId,
            Title = "Pending Cheese",
            OriginalPrice = 30m,
            DiscountedPrice = 15m,
            QuantityAvailable = 5,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            Status = ProductStatus.PendingModeration
        };
        _db.Products.Add(unverifiedProd);
        await _db.SaveChangesAsync();

        var handler = new CreateOrderCommandHandler(_db, _mockAuditLog.Object, _mockNotification.Object);
        var command = new CreateOrderCommand(
            _customerId,
            new List<CheckoutItemRequest> { new(unverifiedProd.Id, 1) },
            "127.0.0.1");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("no longer active or has expired");
    }

    [Fact(DisplayName = "TC-ORD-06: Exact remaining stock depletion succeeds and leaves zero stock")]
    public async Task CreateOrder_ExactStockDepletion_ShouldSucceedWithZeroStock()
    {
        var handler = new CreateOrderCommandHandler(_db, _mockAuditLog.Object, _mockNotification.Object);
        var command = new CreateOrderCommand(
            _customerId,
            new List<CheckoutItemRequest> { new(_productId, 10) }, // exactly 10 available
            "127.0.0.1");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.TotalAmount.Should().Be(300.0m); // 10 * 30.0

        var product = await _db.Products.FindAsync(_productId);
        product!.QuantityAvailable.Should().Be(0);
    }

    [Fact(DisplayName = "TC-ORD-07: Consecutive orders depleting stock to zero blocks subsequent order")]
    public async Task CreateOrder_ConsecutiveDepletion_SubsequentOrderFails()
    {
        var handler = new CreateOrderCommandHandler(_db, _mockAuditLog.Object, _mockNotification.Object);

        // First order takes 8 units (2 remaining)
        var order1 = await handler.Handle(new CreateOrderCommand(
            _customerId, new List<CheckoutItemRequest> { new(_productId, 8) }, "127.0.0.1"), CancellationToken.None);
        order1.Success.Should().BeTrue();

        // Second order takes 2 units (0 remaining)
        var order2 = await handler.Handle(new CreateOrderCommand(
            _customerId, new List<CheckoutItemRequest> { new(_productId, 2) }, "127.0.0.1"), CancellationToken.None);
        order2.Success.Should().BeTrue();

        // Third order tries to take 1 unit -> fails with Insufficient stock
        var order3 = await handler.Handle(new CreateOrderCommand(
            _customerId, new List<CheckoutItemRequest> { new(_productId, 1) }, "127.0.0.1"), CancellationToken.None);
        order3.Success.Should().BeFalse();
        order3.Message.Should().Contain("Insufficient stock");
    }

    [Fact(DisplayName = "TC-ORD-08: Invalid OrderStatus string returns failure in UpdateOrderStatus")]
    public async Task UpdateOrderStatus_InvalidStatusString_ShouldReturnFail()
    {
        var createHandler = new CreateOrderCommandHandler(_db, _mockAuditLog.Object, _mockNotification.Object);
        var orderResult = await createHandler.Handle(new CreateOrderCommand(
            _customerId, new List<CheckoutItemRequest> { new(_productId, 2) }, "127.0.0.1"), CancellationToken.None);

        var updateHandler = new UpdateOrderStatusCommandHandler(_db, _mockAuditLog.Object, _mockNotification.Object);
        var updateCommand = new UpdateOrderStatusCommand(_merchantId, orderResult.Data!.Id, "NonExistentStatus");

        var result = await updateHandler.Handle(updateCommand, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid OrderStatus value");
    }

    [Fact(DisplayName = "TC-ORD-09: Unrelated merchant updating order status returns failure")]
    public async Task UpdateOrderStatus_UnrelatedMerchant_ShouldReturnFail()
    {
        var createHandler = new CreateOrderCommandHandler(_db, _mockAuditLog.Object, _mockNotification.Object);
        var orderResult = await createHandler.Handle(new CreateOrderCommand(
            _customerId, new List<CheckoutItemRequest> { new(_productId, 1) }, "127.0.0.1"), CancellationToken.None);

        // Stranger merchant with their own org
        var strangerId = Guid.NewGuid();
        var strangerOrg = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = strangerId,
            Name = "Stranger Store",
            VerificationStatus = VerificationStatus.Verified
        };
        _db.Organizations.Add(strangerOrg);
        await _db.SaveChangesAsync();

        var updateHandler = new UpdateOrderStatusCommandHandler(_db, _mockAuditLog.Object, _mockNotification.Object);
        var updateCommand = new UpdateOrderStatusCommand(strangerId, orderResult.Data!.Id, "Confirmed");

        var result = await updateHandler.Handle(updateCommand, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Unauthorized access to update this order");
    }

    [Fact(DisplayName = "TC-ORD-10: Multi-item cancellation restores stock for all item lines")]
    public async Task UpdateOrderStatus_CancelMultiItemOrder_RestoresAllStock()
    {
        var prod2 = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CategoryId = _categoryId,
            Title = "Organic Blueberries",
            OriginalPrice = 40.0m,
            DiscountedPrice = 20.0m,
            QuantityAvailable = 15,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            Status = ProductStatus.Active
        };
        _db.Products.Add(prod2);
        await _db.SaveChangesAsync();

        var createHandler = new CreateOrderCommandHandler(_db, _mockAuditLog.Object, _mockNotification.Object);
        var orderResult = await createHandler.Handle(new CreateOrderCommand(
            _customerId,
            new List<CheckoutItemRequest>
            {
                new(_productId, 3), // 10 - 3 = 7
                new(prod2.Id, 5)    // 15 - 5 = 10
            },
            "127.0.0.1"), CancellationToken.None);

        (await _db.Products.FindAsync(_productId))!.QuantityAvailable.Should().Be(7);
        (await _db.Products.FindAsync(prod2.Id))!.QuantityAvailable.Should().Be(10);

        var updateHandler = new UpdateOrderStatusCommandHandler(_db, _mockAuditLog.Object, _mockNotification.Object);
        var cancelResult = await updateHandler.Handle(
            new UpdateOrderStatusCommand(_merchantId, orderResult.Data!.Id, "Cancelled"),
            CancellationToken.None);

        cancelResult.Success.Should().BeTrue();
        cancelResult.Data!.OrderStatus.Should().Be("Cancelled");
        cancelResult.Data.PaymentStatus.Should().Be("Refunded");

        // Verify stock completely restored
        (await _db.Products.FindAsync(_productId))!.QuantityAvailable.Should().Be(10);
        (await _db.Products.FindAsync(prod2.Id))!.QuantityAvailable.Should().Be(15);
    }

    [Theory(DisplayName = "TC-ORD-11: Valid order lifecycle transitions send proper notifications")]
    [InlineData("Confirmed", "NotifOrderConfirmedTitle", "NotifOrderConfirmedBody", "OrderConfirmed")]
    [InlineData("Preparing", "NotifOrderPreparingTitle", "NotifOrderPreparingBody", "OrderPreparing")]
    [InlineData("ReadyForPickup", "NotifOrderReadyForPickupTitle", "NotifOrderReadyForPickupBody", "OrderReadyForPickup")]
    [InlineData("Completed", "NotifOrderCompletedTitle", "NotifOrderCompletedBody", "OrderCompleted")]
    public async Task UpdateOrderStatus_LifecycleTransitions_DispatchesCorrectNotification(
        string status, string expectedTitleKey, string expectedBodyKey, string expectedNotifType)
    {
        var createHandler = new CreateOrderCommandHandler(_db, _mockAuditLog.Object, _mockNotification.Object);
        var orderResult = await createHandler.Handle(new CreateOrderCommand(
            _customerId, new List<CheckoutItemRequest> { new(_productId, 1) }, "127.0.0.1"), CancellationToken.None);

        var updateHandler = new UpdateOrderStatusCommandHandler(_db, _mockAuditLog.Object, _mockNotification.Object);
        var result = await updateHandler.Handle(
            new UpdateOrderStatusCommand(_merchantId, orderResult.Data!.Id, status),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.OrderStatus.Should().Be(status);

        _mockNotification.Verify(n => n.SendNotificationToUserAsync(
            _customerId,
            expectedTitleKey,
            expectedBodyKey,
            expectedNotifType,
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
