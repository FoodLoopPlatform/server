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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Concurrency;

public class HighConcurrencyStressTests : IDisposable
{
    private readonly ApplicationDbContext _db = ApplicationDbContextFactory.Create();
    private readonly Mock<IAuditLogService> _mockAudit = new();
    private readonly Mock<IRealTimeNotificationService> _mockNotification = new();

    public HighConcurrencyStressTests()
    {
    }

    public void Dispose() => _db.Dispose();

    [Fact(DisplayName = "TC-CONC-01: Flash Sale Race - 10 simultaneous checkouts on stock=1 allows exactly 1 to succeed")]
    public async Task CreateOrder_TenConcurrentCheckoutsOnOneStock_OnlyOneSucceeds()
    {
        var merchantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var merchant = new ApplicationUser
        {
            Id = merchantId,
            UserName = "flashmerchant@test.com",
            Email = "flashmerchant@test.com",
            Status = UserStatus.Active
        };
        _db.Users.Add(merchant);

        var org = new Organization
        {
            Id = orgId,
            OwnerId = merchantId,
            Name = "Flash Sale Store",
            VerificationStatus = VerificationStatus.Verified
        };
        _db.Organizations.Add(org);

        var product = new Product
        {
            Id = productId,
            OrganizationId = orgId,
            CategoryId = Guid.NewGuid(),
            Title = "Exclusive Item (1 remaining)",
            OriginalPrice = 100m,
            DiscountedPrice = 50m,
            QuantityAvailable = 1, // Only ONE item available!
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
            Status = ProductStatus.Active
        };
        _db.Products.Add(product);

        // Create 10 distinct customers
        var customerIds = new List<Guid>();
        for (int i = 0; i < 10; i++)
        {
            var cid = Guid.NewGuid();
            customerIds.Add(cid);
            _db.Users.Add(new ApplicationUser
            {
                Id = cid,
                UserName = $"flashbuyer{i}@test.com",
                Email = $"flashbuyer{i}@test.com",
                Status = UserStatus.Active
            });
        }

        await _db.SaveChangesAsync();

        var results = new ConcurrentBag<Result<OrderDto>>();
        var exceptions = new ConcurrentBag<Exception>();

        // Run 10 parallel checkout requests
        var tasks = customerIds.Select(async (cid) =>
        {
            try
            {
                var handler = new CreateOrderCommandHandler(_db, _mockAudit.Object, _mockNotification.Object);
                var command = new CreateOrderCommand(
                    cid,
                    new List<CheckoutItemRequest>
                    {
                        new CheckoutItemRequest(productId, 1)
                    },
                    "127.0.0.1"
                );

                var res = await handler.Handle(command, CancellationToken.None);
                results.Add(res);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        await Task.WhenAll(tasks);

        // Assert that exactly 1 checkout succeeded
        var successfulCheckouts = results.Where(r => r.Success).ToList();
        successfulCheckouts.Should().HaveCount(1, "only 1 customer should obtain the single available stock");

        // The remaining 9 should either have failed gracefully with Success=false or thrown stock exception
        var failedCheckouts = results.Where(r => !r.Success).Count() + exceptions.Count;
        failedCheckouts.Should().Be(9);

        // Assert final inventory in DB is exactly 0 and never negative
        _db.ChangeTracker.Clear();
        var productInDb = await _db.Products.FindAsync(productId);
        productInDb!.QuantityAvailable.Should().Be(0);
    }

    [Fact(DisplayName = "TC-CONC-02: Concurrent Wallet Depletion - 5 simultaneous 100 EGP checkouts on 100 balance allows exactly 1")]
    public async Task WalletCheckout_ConcurrentRequests_PreventsDoubleSpend()
    {
        var customerId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var customer = new ApplicationUser
        {
            Id = customerId,
            UserName = "walletbuyer@test.com",
            Email = "walletbuyer@test.com",
            Status = UserStatus.Active,
            WalletBalance = 100m // Exactly 100 EGP balance!
        };

        var merchant = new ApplicationUser
        {
            Id = merchantId,
            UserName = "walletmerchant@test.com",
            Email = "walletmerchant@test.com",
            Status = UserStatus.Active
        };

        _db.Users.AddRange(customer, merchant);

        var org = new Organization
        {
            Id = orgId,
            OwnerId = merchantId,
            Name = "Wallet Test Store",
            VerificationStatus = VerificationStatus.Verified
        };
        _db.Organizations.Add(org);

        var product = new Product
        {
            Id = productId,
            OrganizationId = orgId,
            CategoryId = Guid.NewGuid(),
            Title = "Wallet Product",
            OriginalPrice = 100m,
            DiscountedPrice = 100m,
            QuantityAvailable = 20,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            Status = ProductStatus.Active
        };
        _db.Products.Add(product);

        // Create 5 distinct orders for this customer, each costing 100 EGP
        var orderIds = new List<Guid>();
        for (int i = 0; i < 5; i++)
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = customerId,
                TotalAmount = 100m,
                OrderStatus = OrderStatus.Pending,
                PaymentStatus = PaymentStatus.Pending
            };
            order.Items.Add(new OrderItem { ProductId = productId, Quantity = 1, UnitPrice = 100m });
            _db.Orders.Add(order);
            orderIds.Add(order.Id);
        }

        await _db.SaveChangesAsync();

        var results = new ConcurrentBag<WalletCheckoutResultDto>();
        var exceptions = new ConcurrentBag<Exception>();

        // Execute 5 simultaneous wallet checkouts
        var tasks = orderIds.Select(async (oid) =>
        {
            try
            {
                var handler = new WalletCheckoutCommandHandler(_db);
                var command = new WalletCheckoutCommand(oid, customerId);
                var res = await handler.Handle(command, CancellationToken.None);
                results.Add(res);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        await Task.WhenAll(tasks);

        // Assert exactly 1 order was successfully paid with wallet
        var successfulPaid = results.Where(r => r.PaymentStatus == "Paid").ToList();
        successfulPaid.Should().HaveCount(1, "wallet had only 100 EGP, so only 1 order could be paid");

        // The other 4 should fail with InsufficientFunds / BadRequest
        var failedCount = results.Where(r => r.PaymentStatus != "Paid").Count() + exceptions.Count;
        failedCount.Should().Be(4);

        // Assert final user wallet balance is 0 and never negative
        _db.ChangeTracker.Clear();
        var userInDb = await _db.Users.FindAsync(customerId);
        userInDb!.WalletBalance.Should().Be(0m);
    }
}
