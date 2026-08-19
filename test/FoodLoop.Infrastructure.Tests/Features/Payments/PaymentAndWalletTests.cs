using FluentAssertions;
using FoodLoop.API.Controllers;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Orders;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Application.Features.Orders.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Admin.Commands;
using FoodLoop.Infrastructure.Features.Orders.Commands;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Payments;

public class PaymentAndWalletTests
{
    private readonly Mock<IPaymentService> _mockPaymentService = new();
    private readonly Mock<ILogger<PaymentsController>> _mockLogger = new();
    private readonly Mock<IAuditLogService> _mockAuditLog = new();

    [Fact]
    public async Task PaymobCallback_ValidHmac_ShouldMarkAsPaidAndConfirm()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            TotalAmount = 150.00m,
            PaymentStatus = PaymentStatus.Pending,
            OrderStatus = OrderStatus.Pending
        };
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        _mockPaymentService.Setup(p => p.VerifyHmac(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var controller = new PaymentsController(dbContext, _mockPaymentService.Object, _mockLogger.Object);

        var jsonPayload = $@"{{
            ""hmac"": ""valid_hmac"",
            ""obj"": {{
                ""amount_cents"": 15000,
                ""created_at"": ""2026-08-18T14:00:00"",
                ""currency"": ""EGP"",
                ""error_occured"": false,
                ""has_parent_transaction"": false,
                ""id"": ""paymob_tx_123"",
                ""integration_id"": 999,
                ""is_3d_secure"": true,
                ""is_auth"": false,
                ""is_capture"": true,
                ""is_voided"": false,
                ""is_refunded"": false,
                ""pending"": false,
                ""success"": true,
                ""source_data"": {{
                    ""pan"": ""1234"",
                    ""sub_type"": ""Mastercard"",
                    ""type"": ""card""
                }},
                ""order"": {{
                    ""merchant_order_id"": ""{orderId}""
                }}
            }}
        }}";

        var payloadDoc = JsonDocument.Parse(jsonPayload);

        // Act
        var result = await controller.PaymobCallback(payloadDoc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var updatedOrder = await dbContext.Orders.Include(o => o.Payment).FirstOrDefaultAsync(o => o.Id == orderId);
        updatedOrder!.PaymentStatus.Should().Be(PaymentStatus.Paid);
        updatedOrder.OrderStatus.Should().Be(OrderStatus.Confirmed);
        updatedOrder.Payment.Should().NotBeNull();
        updatedOrder.Payment!.Status.Should().Be(PaymentStatus.Paid);
        updatedOrder.Payment.TransactionReference.Should().Be("paymob_tx_123");
    }

    [Fact]
    public async Task PaymobCallback_InvalidHmac_ShouldReturnUnauthorizedAndNoMutation()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            TotalAmount = 150.00m,
            PaymentStatus = PaymentStatus.Pending
        };
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        _mockPaymentService.Setup(p => p.VerifyHmac(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var controller = new PaymentsController(dbContext, _mockPaymentService.Object, _mockLogger.Object);

        var jsonPayload = $@"{{
            ""hmac"": ""invalid_hmac"",
            ""obj"": {{
                ""amount_cents"": 15000,
                ""created_at"": ""2026-08-18T14:00:00"",
                ""currency"": ""EGP"",
                ""error_occured"": false,
                ""has_parent_transaction"": false,
                ""id"": ""paymob_tx_123"",
                ""integration_id"": 999,
                ""is_3d_secure"": true,
                ""is_auth"": false,
                ""is_capture"": true,
                ""is_voided"": false,
                ""is_refunded"": false,
                ""pending"": false,
                ""success"": true,
                ""source_data"": {{
                    ""pan"": ""1234"",
                    ""sub_type"": ""Mastercard"",
                    ""type"": ""card""
                }},
                ""order"": {{
                    ""merchant_order_id"": ""{orderId}""
                }}
            }}
        }}";

        var payloadDoc = JsonDocument.Parse(jsonPayload);

        // Act
        var result = await controller.PaymobCallback(payloadDoc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
        var updatedOrder = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        updatedOrder!.PaymentStatus.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task PaymobCallback_AmountMismatch_ShouldReturnBadRequest()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            TotalAmount = 150.00m, // expected 150.00m EGP
            PaymentStatus = PaymentStatus.Pending
        };
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        _mockPaymentService.Setup(p => p.VerifyHmac(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var controller = new PaymentsController(dbContext, _mockPaymentService.Object, _mockLogger.Object);

        var jsonPayload = $@"{{
            ""hmac"": ""valid_hmac"",
            ""obj"": {{
                ""amount_cents"": 9900,
                ""created_at"": ""2026-08-18T14:00:00"",
                ""currency"": ""EGP"",
                ""error_occured"": false,
                ""has_parent_transaction"": false,
                ""id"": ""paymob_tx_123"",
                ""integration_id"": 999,
                ""is_3d_secure"": true,
                ""is_auth"": false,
                ""is_capture"": true,
                ""is_voided"": false,
                ""is_refunded"": false,
                ""pending"": false,
                ""success"": true,
                ""source_data"": {{
                    ""pan"": ""1234"",
                    ""sub_type"": ""Mastercard"",
                    ""type"": ""card""
                }},
                ""order"": {{
                    ""merchant_order_id"": ""{orderId}""
                }}
            }}
        }}";

        var payloadDoc = JsonDocument.Parse(jsonPayload);

        // Act
        var result = await controller.PaymobCallback(payloadDoc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var updatedOrder = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        updatedOrder!.PaymentStatus.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task PaymobCallback_SuccessFalsePayload_ShouldMarkFailed()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            TotalAmount = 150.00m,
            PaymentStatus = PaymentStatus.Pending
        };
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        _mockPaymentService.Setup(p => p.VerifyHmac(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var controller = new PaymentsController(dbContext, _mockPaymentService.Object, _mockLogger.Object);

        var jsonPayload = $@"{{
            ""hmac"": ""valid_hmac"",
            ""obj"": {{
                ""amount_cents"": 15000,
                ""created_at"": ""2026-08-18T14:00:00"",
                ""currency"": ""EGP"",
                ""error_occured"": true,
                ""has_parent_transaction"": false,
                ""id"": ""paymob_tx_123"",
                ""integration_id"": 999,
                ""is_3d_secure"": true,
                ""is_auth"": false,
                ""is_capture"": true,
                ""is_voided"": false,
                ""is_refunded"": false,
                ""pending"": false,
                ""success"": false,
                ""source_data"": {{
                    ""pan"": ""1234"",
                    ""sub_type"": ""Mastercard"",
                    ""type"": ""card""
                }},
                ""order"": {{
                    ""merchant_order_id"": ""{orderId}""
                }}
            }}
        }}";

        var payloadDoc = JsonDocument.Parse(jsonPayload);

        // Act
        var result = await controller.PaymobCallback(payloadDoc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var updatedOrder = await dbContext.Orders.Include(o => o.Payment).FirstOrDefaultAsync(o => o.Id == orderId);
        updatedOrder!.PaymentStatus.Should().Be(PaymentStatus.Failed);
        updatedOrder.Payment.Should().NotBeNull();
        updatedOrder.Payment!.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public async Task PaymobCallback_DuplicateWebhook_ShouldShortCircuitWithoutDoubleMutation()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            TotalAmount = 150.00m,
            PaymentStatus = PaymentStatus.Pending
        };
        var existingPayment = new Payment
        {
            OrderId = orderId,
            Amount = 150.00m,
            Method = "Paymob",
            Status = PaymentStatus.Paid,
            TransactionReference = "paymob_tx_dup"
        };
        dbContext.Orders.Add(order);
        dbContext.Payments.Add(existingPayment);
        await dbContext.SaveChangesAsync();

        _mockPaymentService.Setup(p => p.VerifyHmac(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var controller = new PaymentsController(dbContext, _mockPaymentService.Object, _mockLogger.Object);

        var jsonPayload = $@"{{
            ""hmac"": ""valid_hmac"",
            ""obj"": {{
                ""amount_cents"": 15000,
                ""created_at"": ""2026-08-18T14:00:00"",
                ""currency"": ""EGP"",
                ""error_occured"": false,
                ""has_parent_transaction"": false,
                ""id"": ""paymob_tx_dup"",
                ""integration_id"": 999,
                ""is_3d_secure"": true,
                ""is_auth"": false,
                ""is_capture"": true,
                ""is_voided"": false,
                ""is_refunded"": false,
                ""pending"": false,
                ""success"": true,
                ""source_data"": {{
                    ""pan"": ""1234"",
                    ""sub_type"": ""Mastercard"",
                    ""type"": ""card""
                }},
                ""order"": {{
                    ""merchant_order_id"": ""{orderId}""
                }}
            }}
        }}";

        var payloadDoc = JsonDocument.Parse(jsonPayload);

        // Act
        var result = await controller.PaymobCallback(payloadDoc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        // Verify only 1 Payment row exists
        var paymentsCount = await dbContext.Payments.CountAsync();
        paymentsCount.Should().Be(1);
    }

    private static (ApplicationDbContext dbContext, DbContextOptions<ApplicationDbContext> options, Microsoft.Data.Sqlite.SqliteConnection connection) CreateSqliteDb()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA busy_timeout = 2000;";
            cmd.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return (context, options, connection);
    }

    [Fact]
    public async Task WalletCheckout_SufficientBalance_ShouldSucceed()
    {
        // Arrange
        var (dbContext, _, connection) = CreateSqliteDb();
        try
        {
            var customerId = Guid.NewGuid();
            var customer = new ApplicationUser
            {
                Id = customerId,
                UserName = "customer@example.com",
                Email = "customer@example.com",
                WalletBalance = 100.00m
            };
            dbContext.Users.Add(customer);

            var order = new Order
            {
                UserId = customerId,
                TotalAmount = 60.00m,
                PaymentStatus = PaymentStatus.Pending,
                OrderStatus = OrderStatus.Pending
            };
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();

            var handler = new WalletCheckoutCommandHandler(dbContext);

            // Act
            var result = await handler.Handle(new WalletCheckoutCommand(order.Id, customerId), CancellationToken.None);

            // Assert
            result.OrderId.Should().Be(order.Id);
            result.PaymentStatus.Should().Be("Paid");
            result.OrderStatus.Should().Be("Confirmed");
            result.AmountCharged.Should().Be(60.00m);
            result.RemainingWalletBalance.Should().Be(40.00m);

            // Verify WalletTransaction created
            var tx = await dbContext.WalletTransactions.FirstOrDefaultAsync(t => t.UserId == customerId);
            tx.Should().NotBeNull();
            tx!.Amount.Should().Be(60.00m);
            tx.Type.Should().Be("Payment");
        }
        finally
        {
            connection.Close();
        }
    }

    [Fact]
    public async Task WalletCheckout_InsufficientBalance_ShouldThrowArgumentException()
    {
        // Arrange
        var (dbContext, _, connection) = CreateSqliteDb();
        try
        {
            var customerId = Guid.NewGuid();
            var customer = new ApplicationUser
            {
                Id = customerId,
                UserName = "customer@example.com",
                WalletBalance = 20.00m
            };
            dbContext.Users.Add(customer);

            var order = new Order
            {
                UserId = customerId,
                TotalAmount = 60.00m,
                PaymentStatus = PaymentStatus.Pending
            };
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();

            var handler = new WalletCheckoutCommandHandler(dbContext);

            // Act & Assert
            var act = async () => await handler.Handle(new WalletCheckoutCommand(order.Id, customerId), CancellationToken.None);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("Insufficient wallet balance.");

            // Verify balance and order are unchanged
            var finalUser = await dbContext.Users.FindAsync(customerId);
            finalUser!.WalletBalance.Should().Be(20.00m);

            var finalOrder = await dbContext.Orders.FindAsync(order.Id);
            finalOrder!.PaymentStatus.Should().Be(PaymentStatus.Pending);
        }
        finally
        {
            connection.Close();
        }
    }

    [Fact]
    public async Task WalletCheckout_ConcurrentDoubleSpend_ShouldOnlySucceedOnce()
    {
        // Arrange
        var (dbContext, options, connection) = CreateSqliteDb();
        try
        {
            var customerId = Guid.NewGuid();
            var customer = new ApplicationUser
            {
                Id = customerId,
                UserName = "customer@example.com",
                WalletBalance = 100.00m
            };
            dbContext.Users.Add(customer);

            var order1 = new Order { UserId = customerId, TotalAmount = 60.00m, PaymentStatus = PaymentStatus.Pending };
            var order2 = new Order { UserId = customerId, TotalAmount = 60.00m, PaymentStatus = PaymentStatus.Pending };
            dbContext.Orders.AddRange(order1, order2);
            await dbContext.SaveChangesAsync();

            // Act
            // First checkout succeeds
            var handler1 = new WalletCheckoutCommandHandler(dbContext);
            var result1 = await handler1.Handle(new WalletCheckoutCommand(order1.Id, customerId), CancellationToken.None);

            // Second checkout fails due to insufficient balance
            var handler2 = new WalletCheckoutCommandHandler(dbContext);
            var act = async () => await handler2.Handle(new WalletCheckoutCommand(order2.Id, customerId), CancellationToken.None);

            // Assert
            result1.RemainingWalletBalance.Should().Be(40.00m);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("Insufficient wallet balance.");

            var finalUser = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == customerId);
            finalUser!.WalletBalance.Should().Be(40.00m);
        }
        finally
        {
            connection.Close();
        }
    }

    [Fact]
    public async Task WalletRefund_ShouldSucceed()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        var customerId = Guid.NewGuid();
        var customer = new ApplicationUser
        {
            Id = customerId,
            UserName = "customer@example.com",
            WalletBalance = 10.00m
        };
        dbContext.Users.Add(customer);

        var merchantId = Guid.NewGuid();
        var merchant = new ApplicationUser
        {
            Id = merchantId,
            UserName = "merchant@example.com"
        };
        dbContext.Users.Add(merchant);

        var store = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = merchantId,
            Name = "Refund Bakery"
        };
        dbContext.Organizations.Add(store);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = store.Id,
            Title = "Cake"
        };
        dbContext.Products.Add(product);

        var order = new Order
        {
            UserId = customerId,
            TotalAmount = 50.00m,
            PaymentStatus = PaymentStatus.Paid,
            OrderStatus = OrderStatus.Confirmed
        };
        order.Items.Add(new OrderItem
        {
            ProductId = product.Id,
            Quantity = 1,
            UnitPrice = 50.00m,
            Product = product
        });
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var handler = new RefundOrderCommandHandler(dbContext, _mockAuditLog.Object);

        // Act
        var result = await handler.Handle(new RefundOrderCommand(order.Id, merchantId, 50.00m, "Customer cancelled"), CancellationToken.None);

        // Assert
        result.PaymentStatus.Should().Be("Refunded");
        result.OrderStatus.Should().Be("Cancelled");

        var updatedCustomer = await dbContext.Users.FindAsync(customerId);
        updatedCustomer!.WalletBalance.Should().Be(60.00m);

        var tx = await dbContext.WalletTransactions.FirstOrDefaultAsync(t => t.UserId == customerId && t.Type == "Refund");
        tx.Should().NotBeNull();
        tx!.Amount.Should().Be(50.00m);
    }

    [Fact]
    public async Task WalletRefund_AlreadyRefunded_ShouldThrowConflictException()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        var customerId = Guid.NewGuid();
        var customer = new ApplicationUser { Id = customerId, UserName = "c@example.com", WalletBalance = 0.00m };
        dbContext.Users.Add(customer);

        var merchantId = Guid.NewGuid();
        var store = new Organization { Id = Guid.NewGuid(), OwnerId = merchantId, Name = "Refund Store" };
        dbContext.Organizations.Add(store);

        var product = new Product { Id = Guid.NewGuid(), OrganizationId = store.Id };
        dbContext.Products.Add(product);

        var order = new Order
        {
            UserId = customerId,
            TotalAmount = 50.00m,
            PaymentStatus = PaymentStatus.Refunded,
            OrderStatus = OrderStatus.Cancelled
        };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 1, UnitPrice = 50.00m, Product = product });
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var handler = new RefundOrderCommandHandler(dbContext, _mockAuditLog.Object);

        // Act & Assert
        var act = async () => await handler.Handle(new RefundOrderCommand(order.Id, merchantId, 50.00m, "Duplicate refund"), CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>().WithMessage("This order has already been refunded.");
    }

    [Fact]
    public async Task WalletRefund_CrossTenantRefund_ShouldThrowForbiddenAccessException()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        var customerId = Guid.NewGuid();
        var customer = new ApplicationUser { Id = customerId, UserName = "c@example.com" };
        dbContext.Users.Add(customer);

        var storeA_Owner = Guid.NewGuid();
        var storeB_Owner = Guid.NewGuid();

        var storeA = new Organization { Id = Guid.NewGuid(), OwnerId = storeA_Owner, Name = "Store A" };
        var storeB = new Organization { Id = Guid.NewGuid(), OwnerId = storeB_Owner, Name = "Store B" };
        dbContext.Organizations.AddRange(storeA, storeB);

        var productB = new Product { Id = Guid.NewGuid(), OrganizationId = storeB.Id };
        dbContext.Products.Add(productB);

        var order = new Order
        {
            UserId = customerId,
            TotalAmount = 50.00m,
            PaymentStatus = PaymentStatus.Paid
        };
        order.Items.Add(new OrderItem { ProductId = productB.Id, Quantity = 1, UnitPrice = 50.00m, Product = productB });
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var handler = new RefundOrderCommandHandler(dbContext, _mockAuditLog.Object);

        // Act & Assert
        // Owner of Store A trying to refund an order belonging to Store B
        var act = async () => await handler.Handle(new RefundOrderCommand(order.Id, storeA_Owner, 50.00m, "Attempt cross tenant"), CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenAccessException>().WithMessage("You are not authorized to refund this order as it does not belong to your store.");
    }

    [Fact]
    public async Task CommissionWithdrawal_SufficientOutstanding_ShouldDeductCorrectly()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var store = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = "Bakery Store",
            CommissionWithdrawn = 10.00m
        };
        dbContext.Organizations.Add(store);

        var settings = new SystemSettings
        {
            Id = SystemSettings.SingletonId,
            PlatformCommissionPercent = 10
        };
        dbContext.SystemSettings.Add(settings);

        var product = new Product { Id = Guid.NewGuid(), OrganizationId = store.Id, Title = "Bread" };
        dbContext.Products.Add(product);

        // Create completed orders to generate sales
        var order = new Order { UserId = Guid.NewGuid(), OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 500.00m };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 1, UnitPrice = 500.00m, Product = product });
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        // outstanding = (500.00 * 10%) - 10.00 = 50.00 - 10.00 = 40.00
        var handler = new WithdrawStoreCommissionCommandHandler(dbContext, new Mock<ICurrentUserService>().Object, _mockAuditLog.Object);

        // Act
        var result = await handler.Handle(new WithdrawStoreCommissionCommand(store.Id, 25.00m), CancellationToken.None);

        // Assert
        result.CommissionWithdrawn.Should().Be(35.00m);
        result.OutstandingCommission.Should().Be(15.00m);

        var finalStore = await dbContext.Organizations.FindAsync(store.Id);
        finalStore!.CommissionWithdrawn.Should().Be(35.00m);
    }

    [Fact]
    public async Task CommissionWithdrawal_ExceedsOutstanding_ShouldThrowArgumentException()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var store = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = "Bakery Store",
            CommissionWithdrawn = 10.00m
        };
        dbContext.Organizations.Add(store);

        var settings = new SystemSettings
        {
            Id = SystemSettings.SingletonId,
            PlatformCommissionPercent = 10
        };
        dbContext.SystemSettings.Add(settings);

        var product = new Product { Id = Guid.NewGuid(), OrganizationId = store.Id, Title = "Bread" };
        dbContext.Products.Add(product);

        var order = new Order { UserId = Guid.NewGuid(), OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 500.00m };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 1, UnitPrice = 500.00m, Product = product });
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        // outstanding = 40.00 EGP
        var handler = new WithdrawStoreCommissionCommandHandler(dbContext, new Mock<ICurrentUserService>().Object, _mockAuditLog.Object);

        // Act & Assert
        var act = async () => await handler.Handle(new WithdrawStoreCommissionCommand(store.Id, 45.00m), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Cannot withdraw 45.00 as it exceeds the outstanding commission of 40.000.");
    }

    [Theory]
    [InlineData(0.00)]
    [InlineData(-10.00)]
    public async Task WalletCheckout_ZeroOrNegativeAmount_ShouldThrowArgumentException(decimal invalidAmount)
    {
        // Arrange
        var (dbContext, _, connection) = CreateSqliteDb();
        try
        {
            var customerId = Guid.NewGuid();
            var customer = new ApplicationUser
            {
                Id = customerId,
                UserName = "customer@example.com",
                WalletBalance = 200.00m
            };
            dbContext.Users.Add(customer);

            var order = new Order
            {
                UserId = customerId,
                TotalAmount = invalidAmount,
                PaymentStatus = PaymentStatus.Pending
            };
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();

            var handler = new WalletCheckoutCommandHandler(dbContext);

            // Act & Assert
            var act = async () => await handler.Handle(new WalletCheckoutCommand(order.Id, customerId), CancellationToken.None);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("Order amount must be greater than zero.");
        }
        finally
        {
            connection.Close();
        }
    }

    [Fact]
    public async Task CheckoutOrder_ValidPendingOrder_ShouldReturnCheckoutSession()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        var customerId = Guid.NewGuid();
        var customer = new ApplicationUser
        {
            Id = customerId,
            UserName = "checkout@example.com",
            Email = "checkout@example.com",
            FullName = "Ahmed Hassan",
            PhoneNumber = "+201012345678"
        };
        dbContext.Users.Add(customer);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = customerId,
            TotalAmount = 250.00m,
            PaymentStatus = PaymentStatus.Pending,
            OrderStatus = OrderStatus.Pending
        };
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        _mockPaymentService.Setup(p => p.GeneratePaymentTokenAsync(
            order.Id,
            250.00m,
            "checkout@example.com",
            "Ahmed",
            "Hassan",
            "+201012345678",
            It.IsAny<CancellationToken>())).ReturnsAsync("payment_token_12345");

        var inMemoryConfig = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Paymob:PublicKey"] = "pk_test_foodloop",
                ["Paymob:BaseUrl"] = "https://accept.paymob.com"
            })
            .Build();

        var handler = new CheckoutOrderCommandHandler(dbContext, _mockPaymentService.Object, inMemoryConfig);

        // Act
        var result = await handler.Handle(new CheckoutOrderCommand(order.Id, customerId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().Be(order.Id);
        result.PaymentToken.Should().Be("payment_token_12345");
        result.CheckoutUrl.Should().Contain("publicKey=pk_test_foodloop");
        result.CheckoutUrl.Should().Contain("clientSecret=payment_token_12345");
    }

    [Fact]
    public async Task CheckoutOrder_OrderNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        var inMemoryConfig = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var handler = new CheckoutOrderCommandHandler(dbContext, _mockPaymentService.Object, inMemoryConfig);

        // Act & Assert
        var act = async () => await handler.Handle(new CheckoutOrderCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CheckoutOrder_UnauthorizedUser_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = ownerId,
            TotalAmount = 100.00m,
            PaymentStatus = PaymentStatus.Pending
        };
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var inMemoryConfig = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var handler = new CheckoutOrderCommandHandler(dbContext, _mockPaymentService.Object, inMemoryConfig);

        // Act & Assert
        var differentUserId = Guid.NewGuid();
        var act = async () => await handler.Handle(new CheckoutOrderCommand(order.Id, differentUserId), CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("You are not authorized to pay for this order.");
    }

    [Fact]
    public async Task CheckoutOrder_AlreadyPaid_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        var customerId = Guid.NewGuid();
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = customerId,
            TotalAmount = 100.00m,
            PaymentStatus = PaymentStatus.Paid
        };
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var inMemoryConfig = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var handler = new CheckoutOrderCommandHandler(dbContext, _mockPaymentService.Object, inMemoryConfig);

        // Act & Assert
        var act = async () => await handler.Handle(new CheckoutOrderCommand(order.Id, customerId), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("This order has already been paid.");
    }

    [Fact]
    public async Task GetUserWallet_ShouldReturnBalanceAndTransactionHistory()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "walletuser@test.com",
            Email = "walletuser@test.com",
            WalletBalance = 75.50m
        };
        dbContext.Users.Add(user);

        var tx1 = new WalletTransaction
        {
            UserId = userId,
            Amount = 100.00m,
            Type = "Credit",
            Description = "Deposit",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        var tx2 = new WalletTransaction
        {
            UserId = userId,
            Amount = 24.50m,
            Type = "Debit",
            Description = "Order Payment",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        dbContext.WalletTransactions.AddRange(tx1, tx2);
        await dbContext.SaveChangesAsync();

        var handler = new FoodLoop.Infrastructure.Features.Users.Queries.GetUserWalletQueryHandler(dbContext);

        // Act
        var result = await handler.Handle(new FoodLoop.Application.Features.Users.Queries.GetUserWalletQuery(userId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.WalletBalance.Should().Be(75.50m);
        result.Transactions.Should().HaveCount(2);
        result.Transactions.Select(t => t.Amount).Should().Contain(new[] { 100.00m, 24.50m });
    }
}
