using FluentAssertions;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Application.Features.Orders.Queries;
using FoodLoop.Application.Features.Products.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Admin.Commands;
using FoodLoop.Infrastructure.Features.Admin.Queries;
using FoodLoop.Infrastructure.Features.Orders.Queries;
using FoodLoop.Infrastructure.Features.Products.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Admin;

public class AdminDisputeAndRiskTests
{
    private readonly Mock<IAuditLogService> _mockAudit = new();

    [Fact]
    public async Task GetAllOrders_ShouldReturnPagedOrdersWithUserDetails()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var customerId = Guid.NewGuid();
        var customer = new ApplicationUser { Id = customerId, FullName = "Alice Consumer", UserName = "alice@test.com" };
        db.Users.Add(customer);

        var org = new Organization { Id = Guid.NewGuid(), Name = "Corner Deli", OwnerId = Guid.NewGuid() };
        db.Organizations.Add(org);

        var category = new Category { Id = Guid.NewGuid(), Name = "Snacks" };
        db.Categories.Add(category);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            CategoryId = category.Id,
            Title = "Gourmet Sandwich",
            OriginalPrice = 50m,
            DiscountedPrice = 25m,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1))
        };
        db.Products.Add(product);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = customerId,
            TotalAmount = 50m,
            OrderStatus = OrderStatus.ReadyForPickup,
            PaymentStatus = PaymentStatus.Paid,
            Items = new List<OrderItem>
            {
                new()
                {
                    ProductId = product.Id,
                    Quantity = 2,
                    UnitPrice = 25m
                }
            }
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new GetAllOrdersQueryHandler(db);

        // Act
        var result = await handler.Handle(new GetAllOrdersQuery(PageNumber: 1, PageSize: 10), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result.First();
        dto.Id.Should().Be(order.Id);
        dto.UserFullName.Should().Be("Alice Consumer");
        dto.TotalAmount.Should().Be(50m);
        dto.OrderStatus.Should().Be("ReadyForPickup");
        dto.PaymentStatus.Should().Be("Paid");
        dto.Items.Should().HaveCount(1);
        dto.Items.First().ProductTitle.Should().Be("Gourmet Sandwich");
    }

    [Fact]
    public async Task GetSupportTicketDetail_ShouldReturnTicketWithMessagesAndSenderNames()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var user = new ApplicationUser { Id = userId, FullName = "Bob Customer", Email = "bob@test.com", UserName = "bob@test.com" };
        var admin = new ApplicationUser { Id = adminId, FullName = "Support Agent", Email = "agent@test.com", UserName = "agent@test.com" };
        db.Users.AddRange(user, admin);

        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Category = "Payment",
            Priority = TicketPriority.High,
            Status = TicketStatus.Open,
            Messages = new List<TicketMessage>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    SenderId = userId,
                    Message = "I have a question about my transaction.",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    SenderId = adminId,
                    Message = "We are looking into this for you.",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
                }
            }
        };
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync();

        var handler = new GetSupportTicketDetailQueryHandler(db);

        // Act
        var result = await handler.Handle(new GetSupportTicketDetailQuery(ticket.Id), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(ticket.Id);
        result.UserFullName.Should().Be("Bob Customer");
        result.UserEmail.Should().Be("bob@test.com");
        result.Priority.Should().Be("High");
        result.Messages.Should().HaveCount(2);
        result.Messages.First().SenderName.Should().Be("Bob Customer");
        result.Messages.Last().SenderName.Should().Be("Support Agent");
    }

    [Fact]
    public async Task ResolveDispute_ShouldMarkResolvedAndAuditLog()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        var reporter = new ApplicationUser { Id = reporterId, FullName = "Charlie Reporter", UserName = "charlie@test.com" };
        db.Users.Add(reporter);

        var org = new Organization { Id = Guid.NewGuid(), Name = "Sweet Treats", OwnerId = Guid.NewGuid() };
        db.Organizations.Add(org);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Title = "Cake Pop",
            OriginalPrice = 10m,
            DiscountedPrice = 5m,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1))
        };
        db.Products.Add(product);

        var report = new ProductReport
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            ReportedBy = reporterId,
            Reason = "Expired item sold",
            Details = "Item was already past expiration date.",
            IsResolved = false
        };
        db.ProductReports.Add(report);
        await db.SaveChangesAsync();

        var handler = new ResolveDisputeCommandHandler(db, _mockAudit.Object);

        // Act
        var result = await handler.Handle(new ResolveDisputeCommand(report.Id, adminId, "Refund approved and warning issued"), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsResolved.Should().BeTrue();
        result.AdminNote.Should().Be("Refund approved and warning issued");
        result.ReporterName.Should().Be("Charlie Reporter");
        result.ProductTitle.Should().Be("Cake Pop");

        var updatedReport = await db.ProductReports.FindAsync(report.Id);
        updatedReport!.IsResolved.Should().BeTrue();
        updatedReport.AdminNote.Should().Be("Refund approved and warning issued");

        _mockAudit.Verify(a => a.LogAsync(
            adminId,
            org.Id,
            "DisputeResolved",
            "Product Dispute Resolved",
            It.Is<string>(msg => msg.Contains("Refund approved")),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRiskAnalysis_ShouldCategorizeRiskLevelsCorrectly()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var org = new Organization { Id = Guid.NewGuid(), OwnerId = ownerId, Name = "Fresh Market", IsDeleted = false };
        db.Organizations.Add(org);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var criticalProduct = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Title = "Expiring Tomorrow Milk",
            OriginalPrice = 30m,
            DiscountedPrice = 15m,
            QuantityAvailable = 4,
            Status = ProductStatus.Active,
            ExpirationDate = today.AddDays(1),
            IsDeleted = false
        };
        var mediumProduct = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Title = "Expiring in 5 days Butter",
            OriginalPrice = 50m,
            DiscountedPrice = 40m,
            QuantityAvailable = 2,
            Status = ProductStatus.Active,
            ExpirationDate = today.AddDays(5),
            IsDeleted = false
        };
        var lowProduct = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Title = "Fresh Canned Beans",
            OriginalPrice = 20m,
            DiscountedPrice = 20m,
            QuantityAvailable = 10,
            Status = ProductStatus.Active,
            ExpirationDate = today.AddDays(20),
            IsDeleted = false
        };
        db.Products.AddRange(criticalProduct, mediumProduct, lowProduct);
        await db.SaveChangesAsync();

        var handler = new GetRiskAnalysisQueryHandler(db);

        // Act
        var result = await handler.Handle(new GetRiskAnalysisQuery(ownerId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Summary.TotalActiveProducts.Should().Be(3);
        result.Summary.CriticalCount.Should().Be(1);
        result.Summary.MediumCount.Should().Be(1);
        result.Summary.LowCount.Should().Be(1);
        result.Summary.TotalAtRiskValue.Should().Be((15m * 4) + (40m * 2));
        result.Critical.Should().HaveCount(1);
        result.Critical.First().Title.Should().Be("Expiring Tomorrow Milk");
        result.Medium.Should().HaveCount(1);
        result.Medium.First().Title.Should().Be("Expiring in 5 days Butter");
    }
}
