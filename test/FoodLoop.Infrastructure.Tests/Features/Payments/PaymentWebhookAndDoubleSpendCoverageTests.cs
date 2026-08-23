using FluentAssertions;
using FoodLoop.API.Controllers;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Features.Orders.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Orders.Commands;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Payments;

public class PaymentWebhookAndDoubleSpendCoverageTests : IDisposable
{
    private readonly ApplicationDbContext _db = ApplicationDbContextFactory.Create();
    private readonly Mock<IPaymentService> _mockPaymentService = new();
    private readonly Mock<ILogger<PaymentsController>> _mockLogger = new();
    private readonly Mock<IAuditLogService> _mockAudit = new();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _merchantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public PaymentWebhookAndDoubleSpendCoverageTests()
    {
        var user = new ApplicationUser
        {
            Id = _userId,
            UserName = "user@test.com",
            Email = "user@test.com",
            WalletBalance = 200m,
            Status = UserStatus.Active
        };

        var merchant = new ApplicationUser
        {
            Id = _merchantId,
            UserName = "merchant@test.com",
            Email = "merchant@test.com",
            Status = UserStatus.Active
        };

        var org = new Organization
        {
            Id = _orgId,
            OwnerId = _merchantId,
            Name = "Pay Org",
            VerificationStatus = VerificationStatus.Verified
        };

        _db.Users.AddRange(user, merchant);
        _db.Organizations.Add(org);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact(DisplayName = "TC-PAY-01: Malformed webhook payload returns BadRequest")]
    public async Task PaymobCallback_MalformedPayload_ReturnsBadRequest()
    {
        var controller = new PaymentsController(_db, _mockPaymentService.Object, _mockLogger.Object);
        var invalidJson = JsonDocument.Parse("{\"invalid_key\": 123}").RootElement;

        var result = await controller.PaymobCallback(invalidJson, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact(DisplayName = "TC-PAY-02: Failed HMAC signature returns Unauthorized")]
    public async Task PaymobCallback_InvalidHmac_ReturnsUnauthorized()
    {
        _mockPaymentService.Setup(s => s.VerifyHmac(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        var controller = new PaymentsController(_db, _mockPaymentService.Object, _mockLogger.Object);
        var payloadJson = JsonDocument.Parse(@"{
            ""hmac"": ""invalid_hmac"",
            ""obj"": {
                ""amount_cents"": 5000,
                ""created_at"": ""2026-08-20T00:00:00Z"",
                ""currency"": ""EGP"",
                ""error_occured"": false,
                ""has_parent_transaction"": false,
                ""id"": 123456,
                ""integration_id"": 789,
                ""is_3d_secure"": true,
                ""is_auth"": false,
                ""is_capture"": false,
                ""is_voided"": false,
                ""is_refunded"": false,
                ""pending"": false,
                ""source_data"": { ""pan"": ""2345"", ""sub_type"": ""MasterCard"", ""type"": ""card"" },
                ""success"": true,
                ""order"": { ""merchant_order_id"": """ + Guid.NewGuid() + @""" }
            }
        }").RootElement;

        var result = await controller.PaymobCallback(payloadJson, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact(DisplayName = "TC-PAY-03: Paymob amount mismatch returns BadRequest")]
    public async Task PaymobCallback_AmountMismatch_ReturnsBadRequest()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            TotalAmount = 100.0m, // 100 EGP
            PaymentStatus = PaymentStatus.Pending,
            OrderStatus = OrderStatus.Pending
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        _mockPaymentService.Setup(s => s.VerifyHmac(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        var controller = new PaymentsController(_db, _mockPaymentService.Object, _mockLogger.Object);
        // Callback says 5000 cents (50 EGP), but order is 100 EGP
        var payloadJson = JsonDocument.Parse(@"{
            ""hmac"": ""valid_hmac"",
            ""obj"": {
                ""amount_cents"": 5000,
                ""created_at"": ""2026-08-20T00:00:00Z"",
                ""currency"": ""EGP"",
                ""error_occured"": false,
                ""has_parent_transaction"": false,
                ""id"": 99999,
                ""integration_id"": 789,
                ""is_3d_secure"": true,
                ""is_auth"": false,
                ""is_capture"": false,
                ""is_voided"": false,
                ""is_refunded"": false,
                ""pending"": false,
                ""source_data"": { ""pan"": ""2345"", ""sub_type"": ""MasterCard"", ""type"": ""card"" },
                ""success"": true,
                ""order"": { ""merchant_order_id"": """ + order.Id + @""" }
            }
        }").RootElement;

        var result = await controller.PaymobCallback(payloadJson, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        (result as BadRequestObjectResult)!.Value.Should().Be("Amount mismatch.");
    }

    [Fact(DisplayName = "TC-PAY-04: Successful Paymob callback updates order to Paid & Confirmed")]
    public async Task PaymobCallback_Success_ConfirmsOrderAndSetsPaid()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            TotalAmount = 75.50m, // 75.50 EGP
            PaymentStatus = PaymentStatus.Pending,
            OrderStatus = OrderStatus.Pending
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        _mockPaymentService.Setup(s => s.VerifyHmac(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        var controller = new PaymentsController(_db, _mockPaymentService.Object, _mockLogger.Object);
        // 7550 cents = 75.50 EGP
        var payloadJson = JsonDocument.Parse(@"{
            ""hmac"": ""valid_hmac"",
            ""obj"": {
                ""amount_cents"": 7550,
                ""created_at"": ""2026-08-20T00:00:00Z"",
                ""currency"": ""EGP"",
                ""error_occured"": false,
                ""has_parent_transaction"": false,
                ""id"": 88888,
                ""integration_id"": 789,
                ""is_3d_secure"": true,
                ""is_auth"": false,
                ""is_capture"": false,
                ""is_voided"": false,
                ""is_refunded"": false,
                ""pending"": false,
                ""source_data"": { ""pan"": ""2345"", ""sub_type"": ""MasterCard"", ""type"": ""card"" },
                ""success"": true,
                ""order"": { ""merchant_order_id"": """ + order.Id + @""" }
            }
        }").RootElement;

        var result = await controller.PaymobCallback(payloadJson, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();

        var updatedOrder = await _db.Orders.Include(o => o.Payment).FirstAsync(o => o.Id == order.Id);
        updatedOrder.PaymentStatus.Should().Be(PaymentStatus.Paid);
        updatedOrder.OrderStatus.Should().Be(OrderStatus.Confirmed);
        updatedOrder.Payment.Should().NotBeNull();
        updatedOrder.Payment!.TransactionReference.Should().Be("88888");
        updatedOrder.Payment.Status.Should().Be(PaymentStatus.Paid);
    }

    [Fact(DisplayName = "TC-PAY-05: Duplicate Paymob webhook is safely idempotent and does not create duplicate payment")]
    public async Task PaymobCallback_DuplicateTransaction_IsIdempotent()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            TotalAmount = 50.0m,
            PaymentStatus = PaymentStatus.Pending,
            OrderStatus = OrderStatus.Pending
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        _mockPaymentService.Setup(s => s.VerifyHmac(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        var controller = new PaymentsController(_db, _mockPaymentService.Object, _mockLogger.Object);
        var payloadJson = JsonDocument.Parse(@"{
            ""hmac"": ""valid_hmac"",
            ""obj"": {
                ""amount_cents"": 5000,
                ""created_at"": ""2026-08-20T00:00:00Z"",
                ""currency"": ""EGP"",
                ""error_occured"": false,
                ""has_parent_transaction"": false,
                ""id"": 77777,
                ""integration_id"": 789,
                ""is_3d_secure"": true,
                ""is_auth"": false,
                ""is_capture"": false,
                ""is_voided"": false,
                ""is_refunded"": false,
                ""pending"": false,
                ""source_data"": { ""pan"": ""2345"", ""sub_type"": ""MasterCard"", ""type"": ""card"" },
                ""success"": true,
                ""order"": { ""merchant_order_id"": """ + order.Id + @""" }
            }
        }").RootElement;

        // First callback
        var result1 = await controller.PaymobCallback(payloadJson, CancellationToken.None);
        result1.Should().BeOfType<OkObjectResult>();

        // Second callback (replay)
        var result2 = await controller.PaymobCallback(payloadJson, CancellationToken.None);
        result2.Should().BeOfType<OkObjectResult>();

        // Assert payment record was not duplicated
        var payments = await _db.Payments.Where(p => p.TransactionReference == "77777").ToListAsync();
        payments.Should().HaveCount(1);
    }

    [Fact(DisplayName = "TC-PAY-06: Wallet checkout fails when balance is insufficient")]
    public async Task WalletCheckout_InsufficientBalance_ThrowsArgumentException()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            TotalAmount = 500m, // User only has 200m
            PaymentStatus = PaymentStatus.Pending,
            OrderStatus = OrderStatus.Pending
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var handler = new WalletCheckoutCommandHandler(_db);
        var command = new WalletCheckoutCommand(order.Id, _userId);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Insufficient wallet balance.");

        var user = await _db.Users.FindAsync(_userId);
        user!.WalletBalance.Should().Be(200m); // Balance unchanged
    }

    [Fact(DisplayName = "TC-PAY-07: Wallet checkout succeeds and records wallet transaction")]
    public async Task WalletCheckout_SufficientBalance_DeductsWalletAndConfirmsOrder()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            TotalAmount = 50m, // User has 200m -> leaves 150m
            PaymentStatus = PaymentStatus.Pending,
            OrderStatus = OrderStatus.Pending
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var handler = new WalletCheckoutCommandHandler(_db);
        var command = new WalletCheckoutCommand(order.Id, _userId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.PaymentStatus.Should().Be("Paid");
        result.OrderStatus.Should().Be("Pending");
        result.RemainingWalletBalance.Should().Be(150m);

        var user = await _db.Users.FindAsync(_userId);
        user!.WalletBalance.Should().Be(150m);

        var tx = await _db.WalletTransactions.FirstOrDefaultAsync(t => t.ReferenceId == order.Id.ToString());
        tx.Should().NotBeNull();
        tx!.Amount.Should().Be(50m);
        tx.Type.Should().Be("Payment");
    }

    [Fact(DisplayName = "TC-PAY-08: Paying already paid order with wallet throws ConflictException")]
    public async Task WalletCheckout_AlreadyPaidOrder_ThrowsConflictException()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            TotalAmount = 50m,
            PaymentStatus = PaymentStatus.Paid, // Already paid
            OrderStatus = OrderStatus.Confirmed
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var handler = new WalletCheckoutCommandHandler(_db);
        var command = new WalletCheckoutCommand(order.Id, _userId);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("This order has already been paid.");
    }
}
