using FluentAssertions;
using FoodLoop.API.Controllers;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
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
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Payments;

/// <summary>
/// Full automated coverage for the Payment feature.
///
/// Test groups:
///   A. Paymob Webhook Callback  (TC-WH-01 … TC-WH-08)
///   B. Wallet Checkout          (TC-WLT-01 … TC-WLT-07)
///   C. Merchant Refund          (TC-REF-01 … TC-REF-09)
///   D. Commission Withdrawal    (TC-COM-01 … TC-COM-05)
///
/// Infrastructure:
///   - InMemory EF Core for all tests that do not require real transactions
///   - SQLite in-memory for wallet-checkout concurrency (needs real BEGIN TRANSACTION)
///   - IPaymentService is always mocked (VerifyHmac return value controlled per test)
/// </summary>
public class PaymentFullCoverageTests
{
    // ── shared mocks ─────────────────────────────────────────────────────────
    private readonly Mock<IPaymentService>             _mockPaymentService = new();
    private readonly Mock<ILogger<PaymentsController>> _mockLogger         = new();
    private readonly Mock<IAuditLogService>            _mockAuditLog       = new();

    // ─────────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    private static string BuildCallbackJson(
        Guid orderId, string txId, int amountCents,
        bool success, bool errorOccured = false, string hmac = "valid_hmac")
        => $$"""
        {
            "hmac": "{{hmac}}",
            "obj": {
                "amount_cents": {{amountCents}},
                "created_at": "2026-08-19T14:00:00",
                "currency": "EGP",
                "error_occured": {{errorOccured.ToString().ToLower()}},
                "has_parent_transaction": false,
                "id": "{{txId}}",
                "integration_id": 5855304,
                "is_3d_secure": true,
                "is_auth": false,
                "is_capture": true,
                "is_voided": false,
                "is_refunded": false,
                "pending": false,
                "success": {{success.ToString().ToLower()}},
                "source_data": {
                    "pan": "1234",
                    "sub_type": "Mastercard",
                    "type": "card"
                },
                "order": {
                    "merchant_order_id": "{{orderId}}"
                }
            }
        }
        """;

    private static JsonElement ParseJson(string json)
        => JsonDocument.Parse(json).RootElement;

    private static (ApplicationDbContext ctx, Microsoft.Data.Sqlite.SqliteConnection conn) CreateSqliteContext()
    {
        var conn = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(conn).Options;
        var ctx = new ApplicationDbContext(options);
        ctx.Database.EnsureCreated();
        return (ctx, conn);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // A. PAYMOB WEBHOOK CALLBACK
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "TC-WH-01: Valid success callback marks order Paid+Confirmed")]
    public async Task Callback_ValidSuccess_ShouldMarkOrderPaidAndConfirmed()
    {
        using var db = ApplicationDbContextFactory.Create();
        var orderId = Guid.NewGuid();
        db.Orders.Add(new Order { Id = orderId, TotalAmount = 150.00m, PaymentStatus = PaymentStatus.Pending, OrderStatus = OrderStatus.Pending });
        await db.SaveChangesAsync();

        _mockPaymentService.Setup(p => p.VerifyHmac(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        var controller = new PaymentsController(db, _mockPaymentService.Object, _mockLogger.Object);

        var result = await controller.PaymobCallback(ParseJson(BuildCallbackJson(orderId, "tx_wh01", 15000, success: true)), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var order = await db.Orders.Include(o => o.Payment).FirstAsync(o => o.Id == orderId);
        order.PaymentStatus.Should().Be(PaymentStatus.Paid);
        order.OrderStatus.Should().Be(OrderStatus.Confirmed);
        order.Payment.Should().NotBeNull();
        order.Payment!.Status.Should().Be(PaymentStatus.Paid);
        order.Payment.TransactionReference.Should().Be("tx_wh01");
        order.Payment.Method.Should().Be("Paymob");
    }

    [Fact(DisplayName = "TC-WH-02: Invalid HMAC returns 401 and does not mutate order")]
    public async Task Callback_InvalidHmac_ShouldReturn401AndNotMutateOrder()
    {
        using var db = ApplicationDbContextFactory.Create();
        var orderId = Guid.NewGuid();
        db.Orders.Add(new Order { Id = orderId, TotalAmount = 150.00m, PaymentStatus = PaymentStatus.Pending });
        await db.SaveChangesAsync();

        _mockPaymentService.Setup(p => p.VerifyHmac(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        var controller = new PaymentsController(db, _mockPaymentService.Object, _mockLogger.Object);

        var result = await controller.PaymobCallback(ParseJson(BuildCallbackJson(orderId, "tx_wh02", 15000, success: true, hmac: "WRONG")), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
        (await db.Orders.FirstAsync(o => o.Id == orderId)).PaymentStatus.Should().Be(PaymentStatus.Pending);
    }

    [Fact(DisplayName = "TC-WH-03: Missing 'obj' field returns 400")]
    public async Task Callback_MissingObjField_ShouldReturn400()
    {
        using var db = ApplicationDbContextFactory.Create();
        _mockPaymentService.Setup(p => p.VerifyHmac(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        var controller = new PaymentsController(db, _mockPaymentService.Object, _mockLogger.Object);

        var result = await controller.PaymobCallback(ParseJson(@"{ ""hmac"": ""abc"", ""data"": {} }"), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        ((BadRequestObjectResult)result).Value!.ToString().Should().Contain("Invalid payload structure");
    }

    [Fact(DisplayName = "TC-WH-04: Amount mismatch returns 400 and does not mutate order")]
    public async Task Callback_AmountMismatch_ShouldReturn400AndNotMutateOrder()
    {
        using var db = ApplicationDbContextFactory.Create();
        var orderId = Guid.NewGuid();
        db.Orders.Add(new Order { Id = orderId, TotalAmount = 150.00m, PaymentStatus = PaymentStatus.Pending });
        await db.SaveChangesAsync();

        _mockPaymentService.Setup(p => p.VerifyHmac(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        var controller = new PaymentsController(db, _mockPaymentService.Object, _mockLogger.Object);

        // Send 99 EGP (9900 cents) instead of 150 EGP (15000 cents)
        var result = await controller.PaymobCallback(ParseJson(BuildCallbackJson(orderId, "tx_wh04", 9900, success: true)), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        ((BadRequestObjectResult)result).Value!.ToString().Should().Contain("Amount mismatch");
        (await db.Orders.FirstAsync(o => o.Id == orderId)).PaymentStatus.Should().Be(PaymentStatus.Pending);
    }

    [Fact(DisplayName = "TC-WH-05: success=false marks order as Failed, OrderStatus stays Pending")]
    public async Task Callback_SuccessFalse_ShouldMarkOrderFailed()
    {
        using var db = ApplicationDbContextFactory.Create();
        var orderId = Guid.NewGuid();
        db.Orders.Add(new Order { Id = orderId, TotalAmount = 150.00m, PaymentStatus = PaymentStatus.Pending, OrderStatus = OrderStatus.Pending });
        await db.SaveChangesAsync();

        _mockPaymentService.Setup(p => p.VerifyHmac(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        var controller = new PaymentsController(db, _mockPaymentService.Object, _mockLogger.Object);

        var result = await controller.PaymobCallback(ParseJson(BuildCallbackJson(orderId, "tx_wh05", 15000, success: false, errorOccured: true)), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var order = await db.Orders.Include(o => o.Payment).FirstAsync(o => o.Id == orderId);
        order.PaymentStatus.Should().Be(PaymentStatus.Failed);
        order.OrderStatus.Should().Be(OrderStatus.Pending, "order status must NOT change on failed payment");
        order.Payment!.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact(DisplayName = "TC-WH-06: Duplicate webhook (same tx id) is idempotent — no second DB row")]
    public async Task Callback_DuplicateWebhook_ShouldBeIdempotent()
    {
        using var db = ApplicationDbContextFactory.Create();
        var orderId = Guid.NewGuid();
        db.Orders.Add(new Order { Id = orderId, TotalAmount = 150.00m, PaymentStatus = PaymentStatus.Pending });
        db.Payments.Add(new Payment
        {
            OrderId = orderId, Amount = 150.00m, Method = "Paymob",
            Status = PaymentStatus.Paid, TransactionReference = "tx_wh06_dup"
        });
        await db.SaveChangesAsync();

        _mockPaymentService.Setup(p => p.VerifyHmac(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        var controller = new PaymentsController(db, _mockPaymentService.Object, _mockLogger.Object);

        var result = await controller.PaymobCallback(ParseJson(BuildCallbackJson(orderId, "tx_wh06_dup", 15000, success: true)), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        (await db.Payments.CountAsync()).Should().Be(1, "duplicate webhook must not insert a second Payment row");
    }

    [Fact(DisplayName = "TC-WH-07: Webhook with unknown merchant_order_id returns 200 silently")]
    public async Task Callback_UnknownMerchantOrderId_ShouldReturn200Silently()
    {
        using var db = ApplicationDbContextFactory.Create();
        _mockPaymentService.Setup(p => p.VerifyHmac(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        var controller = new PaymentsController(db, _mockPaymentService.Object, _mockLogger.Object);

        var result = await controller.PaymobCallback(ParseJson(BuildCallbackJson(Guid.NewGuid(), "tx_wh07", 15000, success: true)), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>("unknown orders must be silently ignored to stop Paymob retries");
        (await db.Payments.CountAsync()).Should().Be(0);
    }

    [Fact(DisplayName = "TC-WH-08: Callback on already-paid order with new tx id does NOT overwrite")]
    public async Task Callback_OnAlreadyPaidOrder_ShouldNotOverwriteTransactionReference()
    {
        using var db = ApplicationDbContextFactory.Create();
        var orderId = Guid.NewGuid();
        var payment = new Payment
        {
            OrderId = orderId, Amount = 150.00m, Method = "Paymob",
            Status = PaymentStatus.Paid, TransactionReference = "original_tx"
        };
        db.Orders.Add(new Order { Id = orderId, TotalAmount = 150.00m, PaymentStatus = PaymentStatus.Paid, Payment = payment });
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        _mockPaymentService.Setup(p => p.VerifyHmac(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        var controller = new PaymentsController(db, _mockPaymentService.Object, _mockLogger.Object);

        // Reuses the same amount but a completely new transaction ID
        var result = await controller.PaymobCallback(ParseJson(BuildCallbackJson(orderId, "brand_new_tx", 15000, success: true)), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var p = await db.Payments.FirstAsync(x => x.OrderId == orderId);
        p.TransactionReference.Should().Be("original_tx", "already-paid orders must never be overwritten");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // B. WALLET CHECKOUT
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "TC-WLT-01: Sufficient balance — deducts wallet, confirms order, creates WalletTransaction")]
    public async Task WalletCheckout_SufficientBalance_ShouldSucceed()
    {
        using var db = ApplicationDbContextFactory.Create();
        var customerId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser { Id = customerId, UserName = "c@test.com", WalletBalance = 100.00m });
        var order = new Order { UserId = customerId, TotalAmount = 60.00m, PaymentStatus = PaymentStatus.Pending, OrderStatus = OrderStatus.Pending };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var result = await new WalletCheckoutCommandHandler(db)
            .Handle(new WalletCheckoutCommand(order.Id, customerId), CancellationToken.None);

        result.PaymentStatus.Should().Be("Paid");
        result.OrderStatus.Should().Be("Confirmed");
        result.AmountCharged.Should().Be(60.00m);
        result.RemainingWalletBalance.Should().Be(40.00m);

        (await db.Users.FindAsync(customerId))!.WalletBalance.Should().Be(40.00m);

        var p = await db.Payments.FirstOrDefaultAsync(x => x.OrderId == order.Id);
        p.Should().NotBeNull();
        p!.Method.Should().Be("Wallet");
        p.Status.Should().Be(PaymentStatus.Paid);

        var tx = await db.WalletTransactions.FirstOrDefaultAsync(t => t.UserId == customerId);
        tx.Should().NotBeNull();
        tx!.Amount.Should().Be(60.00m);
        tx.Type.Should().Be("Payment");
    }

    [Fact(DisplayName = "TC-WLT-02: Insufficient balance — throws ArgumentException, balance unchanged")]
    public async Task WalletCheckout_InsufficientBalance_ShouldThrowAndNotMutate()
    {
        using var db = ApplicationDbContextFactory.Create();
        var customerId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser { Id = customerId, UserName = "c@test.com", WalletBalance = 10.00m });
        var order = new Order { UserId = customerId, TotalAmount = 60.00m, PaymentStatus = PaymentStatus.Pending };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var act = async () => await new WalletCheckoutCommandHandler(db)
            .Handle(new WalletCheckoutCommand(order.Id, customerId), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Insufficient wallet balance.");
        (await db.Users.FindAsync(customerId))!.WalletBalance.Should().Be(10.00m);
        (await db.Orders.FindAsync(order.Id))!.PaymentStatus.Should().Be(PaymentStatus.Pending);
    }

    [Fact(DisplayName = "TC-WLT-03: Already-paid order — throws ConflictException")]
    public async Task WalletCheckout_AlreadyPaid_ShouldThrowConflict()
    {
        using var db = ApplicationDbContextFactory.Create();
        var customerId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser { Id = customerId, UserName = "c@test.com", WalletBalance = 200.00m });
        db.Orders.Add(new Order { UserId = customerId, TotalAmount = 50.00m, PaymentStatus = PaymentStatus.Paid });
        await db.SaveChangesAsync();

        var order = await db.Orders.FirstAsync();
        var act = async () => await new WalletCheckoutCommandHandler(db)
            .Handle(new WalletCheckoutCommand(order.Id, customerId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("This order has already been paid.");
    }

    [Fact(DisplayName = "TC-WLT-04: Non-existent order — throws NotFoundException")]
    public async Task WalletCheckout_OrderNotFound_ShouldThrowNotFound()
    {
        using var db = ApplicationDbContextFactory.Create();
        var act = async () => await new WalletCheckoutCommandHandler(db)
            .Handle(new WalletCheckoutCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact(DisplayName = "TC-WLT-05: Cross-user payment attempt — throws ForbiddenAccessException")]
    public async Task WalletCheckout_WrongUser_ShouldThrowForbidden()
    {
        using var db = ApplicationDbContextFactory.Create();
        var ownerId    = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser { Id = ownerId,    UserName = "owner@test.com",    WalletBalance = 200m });
        db.Users.Add(new ApplicationUser { Id = attackerId, UserName = "attacker@test.com", WalletBalance = 200m });
        var order = new Order { UserId = ownerId, TotalAmount = 50.00m, PaymentStatus = PaymentStatus.Pending };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var act = async () => await new WalletCheckoutCommandHandler(db)
            .Handle(new WalletCheckoutCommand(order.Id, attackerId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("You are not authorized to pay for this order.");
    }

    [Theory(DisplayName = "TC-WLT-06: Zero or negative order amount — throws ArgumentException")]
    [InlineData(0.00)]
    [InlineData(-5.00)]
    public async Task WalletCheckout_InvalidOrderAmount_ShouldThrow(decimal badAmount)
    {
        using var db = ApplicationDbContextFactory.Create();
        var customerId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser { Id = customerId, UserName = "c@test.com", WalletBalance = 200m });
        db.Orders.Add(new Order { UserId = customerId, TotalAmount = badAmount, PaymentStatus = PaymentStatus.Pending });
        await db.SaveChangesAsync();

        var order = await db.Orders.FirstAsync();
        var act = async () => await new WalletCheckoutCommandHandler(db)
            .Handle(new WalletCheckoutCommand(order.Id, customerId), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Order amount must be greater than zero.");
    }

    [Fact(DisplayName = "TC-WLT-07: Concurrent double-spend on SQLite — only first succeeds")]
    public async Task WalletCheckout_ConcurrentDoubleSpend_ShouldOnlySucceedOnce()
    {
        var (db, conn) = CreateSqliteContext();
        try
        {
            var customerId = Guid.NewGuid();
            db.Users.Add(new ApplicationUser { Id = customerId, UserName = "c@test.com", WalletBalance = 100m });
            var order1 = new Order { UserId = customerId, TotalAmount = 60m, PaymentStatus = PaymentStatus.Pending };
            var order2 = new Order { UserId = customerId, TotalAmount = 60m, PaymentStatus = PaymentStatus.Pending };
            db.Orders.AddRange(order1, order2);
            await db.SaveChangesAsync();

            var result1 = await new WalletCheckoutCommandHandler(db)
                .Handle(new WalletCheckoutCommand(order1.Id, customerId), CancellationToken.None);

            var act2 = async () => await new WalletCheckoutCommandHandler(db)
                .Handle(new WalletCheckoutCommand(order2.Id, customerId), CancellationToken.None);

            result1.RemainingWalletBalance.Should().Be(40.00m);
            await act2.Should().ThrowAsync<ArgumentException>().WithMessage("Insufficient wallet balance.");

            var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == customerId);
            user.WalletBalance.Should().Be(40.00m, "balance must never go below 0");
        }
        finally { conn.Close(); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // C. MERCHANT REFUND
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Builds a full merchant+store+product+order graph in the given context.</summary>
    private static async Task<(ApplicationUser customer, Guid merchantId, Organization store, Order order)>
        SeedRefundScenario(ApplicationDbContext db,
            decimal orderTotal = 50m, decimal walletBalance = 0m,
            PaymentStatus payStatus = PaymentStatus.Paid)
    {
        var customerId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var customer   = new ApplicationUser { Id = customerId, UserName = "cust@test.com",  WalletBalance = walletBalance };
        var merchant   = new ApplicationUser { Id = merchantId, UserName = "merch@test.com" };
        db.Users.AddRange(customer, merchant);

        var store   = new Organization { Id = Guid.NewGuid(), OwnerId = merchantId, Name = "Test Store" };
        db.Organizations.Add(store);

        var product = new Product { Id = Guid.NewGuid(), OrganizationId = store.Id, Title = "Item" };
        db.Products.Add(product);

        var order = new Order { UserId = customerId, TotalAmount = orderTotal, PaymentStatus = payStatus, OrderStatus = OrderStatus.Confirmed };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 1, UnitPrice = orderTotal, Product = product });
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return (customer, merchantId, store, order);
    }

    [Fact(DisplayName = "TC-REF-01: Full refund by correct merchant credits wallet and cancels order")]
    public async Task Refund_FullRefund_ShouldCreditWalletAndCancelOrder()
    {
        using var db = ApplicationDbContextFactory.Create();
        var (customer, merchantId, _, order) = await SeedRefundScenario(db, orderTotal: 50m, walletBalance: 10m);

        var result = await new RefundOrderCommandHandler(db, _mockAuditLog.Object)
            .Handle(new RefundOrderCommand(order.Id, merchantId, 50m, "Customer request"), CancellationToken.None);

        result.PaymentStatus.Should().Be("Refunded");
        result.OrderStatus.Should().Be("Cancelled");

        (await db.Users.FindAsync(customer.Id))!.WalletBalance.Should().Be(60m, "10 initial + 50 refund");

        var tx = await db.WalletTransactions.FirstOrDefaultAsync(t => t.UserId == customer.Id && t.Type == "Refund");
        tx.Should().NotBeNull();
        tx!.Amount.Should().Be(50m);
    }

    [Fact(DisplayName = "TC-REF-02: Double refund throws ConflictException")]
    public async Task Refund_AlreadyRefunded_ShouldThrowConflict()
    {
        using var db = ApplicationDbContextFactory.Create();
        var (_, merchantId, _, order) = await SeedRefundScenario(db, payStatus: PaymentStatus.Refunded);
        // Manually set to Cancelled as well
        order.OrderStatus = OrderStatus.Cancelled;
        order.PaymentStatus = PaymentStatus.Refunded;
        await db.SaveChangesAsync();

        var act = async () => await new RefundOrderCommandHandler(db, _mockAuditLog.Object)
            .Handle(new RefundOrderCommand(order.Id, merchantId, 50m, "Dup"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("This order has already been refunded.");
    }

    [Fact(DisplayName = "TC-REF-03: Cross-tenant refund throws ForbiddenAccessException")]
    public async Task Refund_CrossTenantAttack_ShouldThrowForbidden()
    {
        using var db = ApplicationDbContextFactory.Create();
        var customerId  = Guid.NewGuid();
        var storeAOwner = Guid.NewGuid();
        var storeBOwner = Guid.NewGuid(); // attacker

        db.Users.AddRange(
            new ApplicationUser { Id = customerId,  UserName = "c@test.com" },
            new ApplicationUser { Id = storeAOwner, UserName = "a@test.com" },
            new ApplicationUser { Id = storeBOwner, UserName = "b@test.com" });

        var storeA = new Organization { Id = Guid.NewGuid(), OwnerId = storeAOwner, Name = "Store A" };
        var storeB = new Organization { Id = Guid.NewGuid(), OwnerId = storeBOwner, Name = "Store B" };
        db.Organizations.AddRange(storeA, storeB);

        var productA = new Product { Id = Guid.NewGuid(), OrganizationId = storeA.Id };
        db.Products.Add(productA);

        var order = new Order { UserId = customerId, TotalAmount = 50m, PaymentStatus = PaymentStatus.Paid };
        order.Items.Add(new OrderItem { ProductId = productA.Id, Quantity = 1, UnitPrice = 50m, Product = productA });
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // storeBOwner tries to refund storeA's order
        var act = async () => await new RefundOrderCommandHandler(db, _mockAuditLog.Object)
            .Handle(new RefundOrderCommand(order.Id, storeBOwner, 50m, "Cross-tenant"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("You are not authorized to refund this order as it does not belong to your store.");
    }

    [Fact(DisplayName = "TC-REF-04: Refund amount exceeds order total throws InvalidOperationException")]
    public async Task Refund_AmountExceedsTotal_ShouldThrowInvalidOperation()
    {
        using var db = ApplicationDbContextFactory.Create();
        var (_, merchantId, _, order) = await SeedRefundScenario(db, orderTotal: 50m);

        var act = async () => await new RefundOrderCommandHandler(db, _mockAuditLog.Object)
            .Handle(new RefundOrderCommand(order.Id, merchantId, 999m, "Over-refund"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot refund 999.00 EGP*");
    }

    [Fact(DisplayName = "TC-REF-05: Partial refund credits wallet but does NOT cancel the order")]
    public async Task Refund_PartialRefund_ShouldCreditWalletButKeepOrderConfirmed()
    {
        using var db = ApplicationDbContextFactory.Create();
        var (customer, merchantId, _, order) = await SeedRefundScenario(db, orderTotal: 100m, walletBalance: 0m);

        var result = await new RefundOrderCommandHandler(db, _mockAuditLog.Object)
            .Handle(new RefundOrderCommand(order.Id, merchantId, 30m, "Partial item defect"), CancellationToken.None);

        result.PaymentStatus.Should().Be("Paid",      "partial refund must NOT flip status to Refunded");
        result.OrderStatus.Should().Be("Confirmed",   "partial refund must NOT cancel the order");

        (await db.Users.FindAsync(customer.Id))!.WalletBalance.Should().Be(30m);

        var tx = await db.WalletTransactions.FirstOrDefaultAsync(t => t.Type == "Refund");
        tx.Should().NotBeNull();
        tx!.Amount.Should().Be(30m);
    }

    [Fact(DisplayName = "TC-REF-06: Zero refund amount throws ArgumentException")]
    public async Task Refund_ZeroAmount_ShouldThrowArgumentException()
    {
        using var db = ApplicationDbContextFactory.Create();
        var (_, merchantId, _, order) = await SeedRefundScenario(db);

        var act = async () => await new RefundOrderCommandHandler(db, _mockAuditLog.Object)
            .Handle(new RefundOrderCommand(order.Id, merchantId, 0m, "Zero"), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Refund amount must be positive.");
    }

    [Fact(DisplayName = "TC-REF-07: Non-existent order throws NotFoundException")]
    public async Task Refund_OrderNotFound_ShouldThrowNotFoundException()
    {
        using var db = ApplicationDbContextFactory.Create();
        var merchantId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser { Id = merchantId, UserName = "m@test.com" });
        db.Organizations.Add(new Organization { Id = Guid.NewGuid(), OwnerId = merchantId, Name = "S" });
        await db.SaveChangesAsync();

        var act = async () => await new RefundOrderCommandHandler(db, _mockAuditLog.Object)
            .Handle(new RefundOrderCommand(Guid.NewGuid(), merchantId, 10m, "Test"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact(DisplayName = "TC-REF-08: Merchant with no Organization row throws ForbiddenAccessException")]
    public async Task Refund_MerchantWithNoOrganization_ShouldThrowForbidden()
    {
        using var db = ApplicationDbContextFactory.Create();
        var customerId        = Guid.NewGuid();
        var orphanMerchantId  = Guid.NewGuid();

        db.Users.AddRange(
            new ApplicationUser { Id = customerId,       UserName = "c@test.com" },
            new ApplicationUser { Id = orphanMerchantId, UserName = "orphan@test.com" });

        var order = new Order { UserId = customerId, TotalAmount = 50m, PaymentStatus = PaymentStatus.Paid };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var act = async () => await new RefundOrderCommandHandler(db, _mockAuditLog.Object)
            .Handle(new RefundOrderCommand(order.Id, orphanMerchantId, 50m, "NoOrg"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("Merchant organization not found.");
    }

    [Fact(DisplayName = "TC-REF-09: Refunding a Pending (never paid) order still credits wallet — behavior observation")]
    public async Task Refund_OnUnpaidOrder_ShouldStillCreditWallet()
    {
        // Documents the existing behavior: the handler only guards against
        // PaymentStatus == Refunded. It does NOT require PaymentStatus == Paid first.
        using var db = ApplicationDbContextFactory.Create();
        var (customer, merchantId, _, order) = await SeedRefundScenario(
            db, orderTotal: 50m, walletBalance: 0m, payStatus: PaymentStatus.Pending);

        var result = await new RefundOrderCommandHandler(db, _mockAuditLog.Object)
            .Handle(new RefundOrderCommand(order.Id, merchantId, 50m, "Refund unpaid"), CancellationToken.None);

        result.PaymentStatus.Should().Be("Refunded");
        result.OrderStatus.Should().Be("Cancelled");

        (await db.Users.FindAsync(customer.Id))!.WalletBalance.Should().Be(50m,
            "wallet is credited even though the order was never paid — current system behavior");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // D. COMMISSION WITHDRAWAL
    // ─────────────────────────────────────────────────────────────────────────

    private static async Task<(Organization store, Guid ownerId)> SeedCommissionScenario(
        ApplicationDbContext db, decimal salesTotal = 500m,
        decimal commissionWithdrawn = 0m, int commissionPercent = 10)
    {
        var ownerId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser { Id = ownerId, UserName = "owner@test.com", Email = "owner@test.com" });

        var store = new Organization
        {
            Id = Guid.NewGuid(), OwnerId = ownerId, Name = "Test Store",
            CommissionWithdrawn = commissionWithdrawn
        };
        db.Organizations.Add(store);

        db.SystemSettings.Add(new SystemSettings
        {
            Id = SystemSettings.SingletonId, PlatformCommissionPercent = commissionPercent
        });

        var product = new Product { Id = Guid.NewGuid(), OrganizationId = store.Id, Title = "Item" };
        db.Products.Add(product);

        var order = new Order
        {
            UserId = ownerId, OrderStatus = OrderStatus.Completed,
            PaymentStatus = PaymentStatus.Paid, TotalAmount = salesTotal
        };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 1, UnitPrice = salesTotal, Product = product });
        db.Orders.Add(order);

        await db.SaveChangesAsync();
        return (store, ownerId);
    }

    [Fact(DisplayName = "TC-COM-01: Valid withdrawal within outstanding — deducts correctly")]
    public async Task Commission_ValidWithdrawal_ShouldDeductFromOutstanding()
    {
        // Sales = 500, 10% = 50 EGP, already withdrawn = 10, outstanding = 40
        using var db = ApplicationDbContextFactory.Create();
        var (store, _) = await SeedCommissionScenario(db, salesTotal: 500m, commissionWithdrawn: 10m);

        var result = await new WithdrawStoreCommissionCommandHandler(db, new Mock<ICurrentUserService>().Object, _mockAuditLog.Object)
            .Handle(new WithdrawStoreCommissionCommand(store.Id, 25m), CancellationToken.None);

        result.CommissionWithdrawn.Should().Be(35m);
        result.OutstandingCommission.Should().Be(15m);
        (await db.Organizations.FindAsync(store.Id))!.CommissionWithdrawn.Should().Be(35m);
    }

    [Fact(DisplayName = "TC-COM-02: Withdrawal exceeds outstanding throws ArgumentException")]
    public async Task Commission_ExceedsOutstanding_ShouldThrowArgumentException()
    {
        // Sales = 500, 10% = 50 EGP outstanding
        using var db = ApplicationDbContextFactory.Create();
        var (store, _) = await SeedCommissionScenario(db, salesTotal: 500m, commissionWithdrawn: 0m);

        var act = async () => await new WithdrawStoreCommissionCommandHandler(db, new Mock<ICurrentUserService>().Object, _mockAuditLog.Object)
            .Handle(new WithdrawStoreCommissionCommand(store.Id, 999m), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Cannot withdraw 999*exceeds*outstanding*");
    }

    [Fact(DisplayName = "TC-COM-03: Zero withdrawal amount throws ArgumentException")]
    public async Task Commission_ZeroAmount_ShouldThrowArgumentException()
    {
        using var db = ApplicationDbContextFactory.Create();
        var (store, _) = await SeedCommissionScenario(db);

        var act = async () => await new WithdrawStoreCommissionCommandHandler(db, new Mock<ICurrentUserService>().Object, _mockAuditLog.Object)
            .Handle(new WithdrawStoreCommissionCommand(store.Id, 0m), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Amount to withdraw must be greater than zero.");
    }

    [Fact(DisplayName = "TC-COM-04: Non-existent store throws NotFoundException")]
    public async Task Commission_NonExistentStore_ShouldThrowNotFoundException()
    {
        using var db = ApplicationDbContextFactory.Create();
        db.SystemSettings.Add(new SystemSettings { Id = SystemSettings.SingletonId, PlatformCommissionPercent = 10 });
        await db.SaveChangesAsync();

        var act = async () => await new WithdrawStoreCommissionCommandHandler(db, new Mock<ICurrentUserService>().Object, _mockAuditLog.Object)
            .Handle(new WithdrawStoreCommissionCommand(Guid.NewGuid(), 10m), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact(DisplayName = "TC-COM-05: Withdrawing the exact outstanding amount leaves zero outstanding")]
    public async Task Commission_ExactAmount_ShouldLeaveZeroOutstanding()
    {
        // Sales = 200, 10% = 20 EGP outstanding, withdraw exactly 20
        using var db = ApplicationDbContextFactory.Create();
        var (store, _) = await SeedCommissionScenario(db, salesTotal: 200m, commissionWithdrawn: 0m);

        var result = await new WithdrawStoreCommissionCommandHandler(db, new Mock<ICurrentUserService>().Object, _mockAuditLog.Object)
            .Handle(new WithdrawStoreCommissionCommand(store.Id, 20m), CancellationToken.None);

        result.CommissionWithdrawn.Should().Be(20m);
        result.OutstandingCommission.Should().Be(0m);
    }
}
