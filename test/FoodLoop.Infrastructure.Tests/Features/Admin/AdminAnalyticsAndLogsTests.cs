using FluentAssertions;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Admin.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Admin;

public class AdminAnalyticsAndLogsTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager = MockUserManagerFactory.Create();

    [Fact]
    public async Task GetAnalyticsSummary_ShouldAggregatePlatformMetricsAccurately()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();

        // 1. Seed Roles & Users
        var customerRole = new ApplicationRole(AppRole.Customer) { Id = Guid.NewGuid(), NormalizedName = AppRole.Customer.ToUpper() };
        var merchantRole = new ApplicationRole(AppRole.Merchant) { Id = Guid.NewGuid(), NormalizedName = AppRole.Merchant.ToUpper() };
        var charityRole = new ApplicationRole(AppRole.Charity) { Id = Guid.NewGuid(), NormalizedName = AppRole.Charity.ToUpper() };
        var adminRole = new ApplicationRole(AppRole.Admin) { Id = Guid.NewGuid(), NormalizedName = AppRole.Admin.ToUpper() };

        db.Roles.AddRange(customerRole, merchantRole, charityRole, adminRole);

        var customerUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = "cust@test.com", Email = "cust@test.com", FullName = "Customer User", Status = UserStatus.Active, CreatedAt = DateTimeOffset.UtcNow.AddDays(-10) };
        var merchantUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = "merch@test.com", Email = "merch@test.com", FullName = "Merchant User", Status = UserStatus.Active, CreatedAt = DateTimeOffset.UtcNow.AddDays(-20) };
        var charityUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = "charity@test.com", Email = "charity@test.com", FullName = "Charity User", Status = UserStatus.Active, CreatedAt = DateTimeOffset.UtcNow.AddDays(-30) };
        var adminUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = "admin@test.com", Email = "admin@test.com", FullName = "Admin User", Status = UserStatus.Active, CreatedAt = DateTimeOffset.UtcNow.AddDays(-40) };

        db.Users.AddRange(customerUser, merchantUser, charityUser, adminUser);

        db.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = customerUser.Id, RoleId = customerRole.Id },
            new IdentityUserRole<Guid> { UserId = merchantUser.Id, RoleId = merchantRole.Id },
            new IdentityUserRole<Guid> { UserId = charityUser.Id, RoleId = charityRole.Id },
            new IdentityUserRole<Guid> { UserId = adminUser.Id, RoleId = adminRole.Id }
        );

        // 2. Seed Stores
        var store1 = new Organization { Id = Guid.NewGuid(), OwnerId = merchantUser.Id, Name = "Store 1", VerificationStatus = VerificationStatus.Verified };
        var store2 = new Organization { Id = Guid.NewGuid(), OwnerId = merchantUser.Id, Name = "Store 2", VerificationStatus = VerificationStatus.Pending };
        var charityOrg = new Organization { Id = Guid.NewGuid(), OwnerId = charityUser.Id, Name = "Charity 1", VerificationStatus = VerificationStatus.Verified };

        db.Organizations.AddRange(store1, store2, charityOrg);

        // 3. Seed Products
        var p1 = new Product { Id = Guid.NewGuid(), OrganizationId = store1.Id, Title = "Bread", OriginalPrice = 20m, DiscountedPrice = 10m, QuantityAvailable = 5, Status = ProductStatus.Active, ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)) };
        var p2 = new Product { Id = Guid.NewGuid(), OrganizationId = store1.Id, Title = "Milk", OriginalPrice = 30m, DiscountedPrice = 15m, QuantityAvailable = 10, Status = ProductStatus.Active, ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3)) };
        db.Products.AddRange(p1, p2);

        // 4. Seed Orders & Payments
        var order1 = new Order
        {
            Id = Guid.NewGuid(),
            UserId = customerUser.Id,
            TotalAmount = 50m,
            OrderStatus = OrderStatus.Completed,
            PaymentStatus = PaymentStatus.Paid,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            Items = new List<OrderItem>
            {
                new OrderItem { ProductId = p1.Id, Quantity = 2, UnitPrice = 10m, Product = p1 },
                new OrderItem { ProductId = p2.Id, Quantity = 2, UnitPrice = 15m, Product = p2 }
            },
            Payment = new Payment { OrderId = Guid.NewGuid(), Amount = 50m, Method = "Paymob", Status = PaymentStatus.Paid, TransactionReference = "TX-01" }
        };

        var order2 = new Order
        {
            Id = Guid.NewGuid(),
            UserId = customerUser.Id,
            TotalAmount = 30m,
            OrderStatus = OrderStatus.Confirmed,
            PaymentStatus = PaymentStatus.Paid,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            Items = new List<OrderItem>
            {
                new OrderItem { ProductId = p2.Id, Quantity = 2, UnitPrice = 15m, Product = p2 }
            },
            Payment = new Payment { OrderId = Guid.NewGuid(), Amount = 30m, Method = "Wallet", Status = PaymentStatus.Paid, TransactionReference = "TX-02" }
        };

        db.Orders.AddRange(order1, order2);

        // 5. Seed Disputes / Product Reports
        var report = new ProductReport
        {
            Id = Guid.NewGuid(),
            ProductId = p1.Id,
            ReportedBy = customerUser.Id,
            Reason = "Quality issue",
            IsResolved = false,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        db.ProductReports.Add(report);

        await db.SaveChangesAsync();

        var handler = new GetAnalyticsSummaryQueryHandler(db);

        // Act
        var result = await handler.Handle(new GetAnalyticsSummaryQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Users.Total.Should().Be(4);
        result.Users.Customers.Should().Be(1);
        result.Users.Merchants.Should().Be(1);
        result.Users.Charities.Should().Be(1);
        result.Users.Admins.Should().Be(1);

        result.Organizations.Total.Should().Be(3);
        result.Organizations.Verified.Should().Be(2);
        result.Organizations.Pending.Should().Be(1);

        result.TotalRevenue.Should().Be(80m); // 50 (order1) + 30 (order2)
        result.Orders.Total.Should().Be(2);
        result.Orders.Completed.Should().Be(1);
    }

    [Fact]
    public async Task GetAdminActivityLogs_ShouldFilterAndPaginateCorrectly()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var adminUser = new ApplicationUser { Id = adminId, UserName = "superadmin@test.com", FullName = "Super Admin" };
        db.Users.Add(adminUser);

        _mockUserManager.Setup(m => m.Users).Returns(new List<ApplicationUser> { adminUser }.AsQueryable());

        var log1 = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = adminId,
            EventType = "DocumentVerified",
            Title = "Store verified",
            Description = "Store 1 was approved",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2)
        };

        var log2 = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = adminId,
            EventType = "UserStatusUpdated",
            Title = "User deactivated",
            Description = "User account banned",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        var log3 = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            EventType = "CustomerOrderPlaced", // Non-admin event
            Title = "Order Placed",
            Description = "Customer placed an order",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        };

        db.AuditLogs.AddRange(log1, log2, log3);
        await db.SaveChangesAsync();

        var handler = new GetAdminActivityLogsQueryHandler(db, _mockUserManager.Object);

        // Act - Query all admin activity logs
        var resultAll = await handler.Handle(new GetAdminActivityLogsQuery(PageNumber: 1, PageSize: 10), CancellationToken.None);

        // Assert
        resultAll.Should().NotBeNull();
        resultAll.TotalCount.Should().Be(2);
        resultAll.Items.Should().HaveCount(2);
        resultAll.Items.Select(i => i.EventType).Should().Contain(new[] { "DocumentVerified", "UserStatusUpdated" });

        // Act - Query with search term
        var resultSearch = await handler.Handle(new GetAdminActivityLogsQuery(SearchTerm: "banned", PageNumber: 1, PageSize: 10), CancellationToken.None);
        resultSearch.TotalCount.Should().Be(1);
        resultSearch.Items.First().Title.Should().Be("User deactivated");

        // Act - Query with admin filter
        var resultAdmin = await handler.Handle(new GetAdminActivityLogsQuery(AdminUserId: adminId, PageNumber: 1, PageSize: 10), CancellationToken.None);
        resultAdmin.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetPlatformActivityLogs_ShouldFilterByEventTypeAndOrganization()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var org = new Organization { Id = orgId, Name = "Fresh Bakery", OwnerId = userId };
        var user = new ApplicationUser { Id = userId, UserName = "baker@test.com", FullName = "Baker Bob" };

        db.Organizations.Add(org);
        db.Users.Add(user);

        var log1 = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = orgId,
            EventType = "ProductAdded",
            Title = "New Croissant",
            Description = "Croissant listed",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        var log2 = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = "ProfileUpdated",
            Title = "Phone updated",
            Description = "User changed phone",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        db.AuditLogs.AddRange(log1, log2);
        await db.SaveChangesAsync();

        var handler = new GetPlatformActivityLogsQueryHandler(db);

        // Act - Query by OrganizationId
        var resultOrg = await handler.Handle(new GetPlatformActivityLogsQuery { OrganizationId = orgId, PageNumber = 1, PageSize = 10 }, CancellationToken.None);

        // Assert
        resultOrg.Should().HaveCount(1);
        resultOrg.First().OrganizationName.Should().Be("Fresh Bakery");
        resultOrg.First().UserName.Should().Be("Baker Bob");

        // Act - Query by EventType
        var resultEvent = await handler.Handle(new GetPlatformActivityLogsQuery { EventType = "ProfileUpdated", PageNumber = 1, PageSize = 10 }, CancellationToken.None);
        resultEvent.Should().HaveCount(1);
        resultEvent.First().EventType.Should().Be("ProfileUpdated");
    }
}
