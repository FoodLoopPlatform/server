using FluentAssertions;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Reviews;
using FoodLoop.Application.Features.Reviews.Commands;
using FoodLoop.Application.Features.SupportTickets.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Reviews.Commands;
using FoodLoop.Infrastructure.Features.SupportTickets.Commands;
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

namespace FoodLoop.Infrastructure.Tests.Features.Reviews;

public class ReviewAndSupportTicketBoundaryTests : IDisposable
{
    private readonly ApplicationDbContext _db = ApplicationDbContextFactory.Create();
    private readonly Mock<IAuditLogService> _mockAudit = new();

    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _strangerCustomerId = Guid.NewGuid();
    private readonly Guid _merchantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();

    public ReviewAndSupportTicketBoundaryTests()
    {
        var customer = new ApplicationUser
        {
            Id = _customerId,
            UserName = "customer@reviewtest.com",
            Email = "customer@reviewtest.com",
            FullName = "Reviewing Customer",
            Status = UserStatus.Active
        };

        var stranger = new ApplicationUser
        {
            Id = _strangerCustomerId,
            UserName = "stranger@reviewtest.com",
            Email = "stranger@reviewtest.com",
            FullName = "Stranger Customer",
            Status = UserStatus.Active
        };

        var merchant = new ApplicationUser
        {
            Id = _merchantId,
            UserName = "merchant@reviewtest.com",
            Email = "merchant@reviewtest.com",
            Status = UserStatus.Active
        };

        _db.Users.AddRange(customer, stranger, merchant);

        var org = new Organization
        {
            Id = _orgId,
            OwnerId = _merchantId,
            Name = "Review Target Bakery",
            VerificationStatus = VerificationStatus.Verified
        };
        _db.Organizations.Add(org);

        var product = new Product
        {
            Id = _productId,
            OrganizationId = _orgId,
            CategoryId = Guid.NewGuid(),
            Title = "Fresh Croissant",
            OriginalPrice = 20m,
            DiscountedPrice = 10m,
            QuantityAvailable = 5,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            Status = ProductStatus.Active
        };
        _db.Products.Add(product);

        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact(DisplayName = "TC-REV-01: Reviewing an order with Pending/Cancelled status returns failure")]
    public async Task SubmitReview_UncompletedOrder_ReturnsFail()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = _customerId,
            TotalAmount = 10m,
            OrderStatus = OrderStatus.Pending, // Not Completed!
            PaymentStatus = PaymentStatus.Pending
        };
        order.Items.Add(new OrderItem { ProductId = _productId, Quantity = 1, UnitPrice = 10m });
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var handler = new SubmitReviewCommandHandler(_db, _mockAudit.Object);
        var command = new SubmitReviewCommand(_customerId, order.Id, 5, "Great!");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("You can only review completed orders");
    }

    [Fact(DisplayName = "TC-REV-02: Stranger customer reviewing someone else's order returns failure")]
    public async Task SubmitReview_StrangerOrder_ReturnsFail()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = _customerId, // Belongs to Customer A
            TotalAmount = 10m,
            OrderStatus = OrderStatus.Completed,
            PaymentStatus = PaymentStatus.Paid
        };
        order.Items.Add(new OrderItem { ProductId = _productId, Quantity = 1, UnitPrice = 10m });
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var handler = new SubmitReviewCommandHandler(_db, _mockAudit.Object);
        // Stranger attempts to review Customer A's order
        var command = new SubmitReviewCommand(_strangerCustomerId, order.Id, 5, "I didn't buy this!");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("You can only review your own orders");
    }

    [Fact(DisplayName = "TC-REV-03: Double review on same completed order returns failure")]
    public async Task SubmitReview_DuplicateReview_ReturnsFail()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = _customerId,
            TotalAmount = 10m,
            OrderStatus = OrderStatus.Completed,
            PaymentStatus = PaymentStatus.Paid
        };
        order.Items.Add(new OrderItem { ProductId = _productId, Quantity = 1, UnitPrice = 10m });
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var handler = new SubmitReviewCommandHandler(_db, _mockAudit.Object);

        // First review succeeds
        var firstResult = await handler.Handle(new SubmitReviewCommand(_customerId, order.Id, 5, "First"), CancellationToken.None);
        firstResult.Success.Should().BeTrue();

        // Second review fails
        var secondResult = await handler.Handle(new SubmitReviewCommand(_customerId, order.Id, 4, "Second"), CancellationToken.None);
        secondResult.Success.Should().BeFalse();
        secondResult.Message.Should().Contain("already been reviewed");
    }

    [Fact(DisplayName = "TC-SUP-01: Replying to non-existent support ticket throws NotFoundException")]
    public async Task CustomerReply_NonExistentTicket_ThrowsNotFoundException()
    {
        var handler = new CustomerReplyToSupportTicketCommandHandler(_db);
        var command = new CustomerReplyToSupportTicketCommand(_customerId, Guid.NewGuid(), "Hello?");

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact(DisplayName = "TC-SUP-02: Stranger replying to another user's support ticket returns unauthorized failure")]
    public async Task CustomerReply_StrangerTicket_ReturnsUnauthorizedFail()
    {
        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(),
            UserId = _customerId, // User A's ticket
            Category = "General",
            Status = TicketStatus.Open,
            Priority = TicketPriority.Low
        };
        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync();

        var handler = new CustomerReplyToSupportTicketCommandHandler(_db);
        // Stranger attempts to reply to User A's ticket
        var command = new CustomerReplyToSupportTicketCommand(_strangerCustomerId, ticket.Id, "Intruder message");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized to reply to this ticket.");
    }

    [Fact(DisplayName = "TC-SUP-03: Replying to a closed or resolved support ticket returns failure")]
    public async Task CustomerReply_ResolvedTicket_ReturnsFail()
    {
        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(),
            UserId = _customerId,
            Category = "General",
            Status = TicketStatus.Resolved, // Resolved!
            Priority = TicketPriority.Low
        };
        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync();

        var handler = new CustomerReplyToSupportTicketCommandHandler(_db);
        var command = new CustomerReplyToSupportTicketCommand(_customerId, ticket.Id, "Reopening message?");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Cannot reply to a closed or resolved ticket.");
    }
}
