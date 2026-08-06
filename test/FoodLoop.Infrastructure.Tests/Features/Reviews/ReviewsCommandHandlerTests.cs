using FluentAssertions;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Reviews;
using FoodLoop.Application.Features.Reviews.Commands;
using FoodLoop.Application.Features.Reviews.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Reviews.Commands;
using FoodLoop.Infrastructure.Features.Reviews.Queries;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Reviews;

public class ReviewsCommandHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _db = ApplicationDbContextFactory.Create();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _orderId = Guid.NewGuid();
    private readonly Mock<IAuditLogService> _auditLog = new();

    public ReviewsCommandHandlerTests()
    {
        // Seed database
        var customer = new Identity.ApplicationUser
        {
            Id = _customerId,
            UserName = "customer@example.com",
            Email = "customer@example.com",
            FullName = "Customer Reviewer"
        };
        _db.Users.Add(customer);

        var org = new Organization
        {
            Id = _organizationId,
            OwnerId = Guid.NewGuid(),
            Name = "Reviews Test Org",
            VerificationStatus = VerificationStatus.Verified
        };
        _db.Organizations.Add(org);

        var product = new Product
        {
            Id = _productId,
            OrganizationId = _organizationId,
            CategoryId = Guid.NewGuid(),
            Title = "Product Review Test",
            OriginalPrice = 10.0m,
            DiscountedPrice = 5.0m,
            QuantityAvailable = 2,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            Status = ProductStatus.Active
        };
        _db.Products.Add(product);

        var order = new Order
        {
            Id = _orderId,
            UserId = _customerId,
            TotalAmount = 5.0m,
            OrderStatus = OrderStatus.Completed,
            PaymentStatus = PaymentStatus.Paid
        };

        var orderItem = new OrderItem
        {
            OrderId = _orderId,
            ProductId = _productId,
            Quantity = 1,
            UnitPrice = 5.0m
        };
        order.Items.Add(orderItem);

        _db.Orders.Add(order);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SubmitReview_should_save_to_database_and_return_dto()
    {
        // Arrange
        var handler = new SubmitReviewCommandHandler(_db, _auditLog.Object);
        var command = new SubmitReviewCommand(_customerId, _orderId, 5, "Awesome service!");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Rating.Should().Be(5);
        result.Data.Comment.Should().Be("Awesome service!");
        result.Data.OrganizationName.Should().Be("Reviews Test Org");

        // Verify audit log
        _auditLog.Verify(a => a.LogAsync(
            _customerId,
            _organizationId,
            "ReviewSubmitted",
            "Review Submitted",
            It.IsAny<string>(),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitReview_should_fail_if_already_reviewed()
    {
        // Arrange
        var handler = new SubmitReviewCommandHandler(_db, _auditLog.Object);
        var command = new SubmitReviewCommand(_customerId, _orderId, 5, "First review");
        await handler.Handle(command, CancellationToken.None);

        var secondCommand = new SubmitReviewCommand(_customerId, _orderId, 4, "Second review");

        // Act
        var result = await handler.Handle(secondCommand, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already been reviewed");
    }

    [Fact]
    public async Task GetOrganizationReviews_should_return_reviews_correctly()
    {
        // Arrange
        var handler = new SubmitReviewCommandHandler(_db, _auditLog.Object);
        await handler.Handle(new SubmitReviewCommand(_customerId, _orderId, 4, "Not bad"), CancellationToken.None);

        var queryHandler = new GetOrganizationReviewsQueryHandler(_db);
        var query = new GetOrganizationReviewsQuery(_organizationId, 1, 10);

        // Act
        var result = await queryHandler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().Rating.Should().Be(4);
        result.First().Comment.Should().Be("Not bad");
    }
}
