using FluentAssertions;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Admin.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Tests.TestSupport;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Admin;

public class AdminProductsAndSettingsTests
{
    [Fact]
    public async Task GetActivityLogById_ValidId_ShouldReturnDetailedEntryWithSeverityAndActor()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        var adminUser = new ApplicationUser { Id = adminId, UserName = "admin@test.com", FullName = "Admin Alex" };
        var store = new Organization { Id = orgId, Name = "Mega Supermarket", OwnerId = adminId };

        db.Users.Add(adminUser);
        db.Organizations.Add(store);

        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = adminId,
            OrganizationId = orgId,
            EventType = "ProductModerated",
            Title = "Product Removed",
            Description = "Product violated safety rules",
            IpAddress = "192.168.1.1",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-15)
        };
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();

        var handler = new GetActivityLogByIdQueryHandler(db);

        // Act
        var result = await handler.Handle(new GetActivityLogByIdQuery(log.Id), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(log.Id);
        result.UserName.Should().Be("Admin Alex");
        result.ActorType.Should().Be("Admin");
        result.OrganizationName.Should().Be("Mega Supermarket");
        result.EventType.Should().Be("ProductModerated");
        result.Severity.Should().Be("Medium");
        result.IpAddress.Should().Be("192.168.1.1");
    }

    [Fact]
    public async Task GetActivityLogById_NonExistent_ShouldThrowNotFoundException()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var handler = new GetActivityLogByIdQueryHandler(db);

        // Act & Assert
        var act = async () => await handler.Handle(new GetActivityLogByIdQuery(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetAdminProducts_ShouldFilterByStatusAndOrganization()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var category = new Category { Id = Guid.NewGuid(), Name = "Bakery & Dairy" };
        db.Categories.Add(category);

        var org1 = new Organization { Id = Guid.NewGuid(), Name = "Store Alpha", OwnerId = Guid.NewGuid() };
        var org2 = new Organization { Id = Guid.NewGuid(), Name = "Store Beta", OwnerId = Guid.NewGuid() };
        db.Organizations.AddRange(org1, org2);

        var p1 = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = org1.Id,
            CategoryId = category.Id,
            Title = "Fresh Bread",
            OriginalPrice = 20m,
            DiscountedPrice = 10m,
            QuantityAvailable = 5,
            Status = ProductStatus.Active,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1))
        };
        var p2 = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = org1.Id,
            CategoryId = category.Id,
            Title = "Old Cheese",
            OriginalPrice = 40m,
            DiscountedPrice = 20m,
            QuantityAvailable = 0,
            Status = ProductStatus.SoldOut,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2))
        };
        var p3 = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = org2.Id,
            CategoryId = category.Id,
            Title = "Greek Yogurt",
            OriginalPrice = 30m,
            DiscountedPrice = 15m,
            QuantityAvailable = 8,
            Status = ProductStatus.Active,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3))
        };
        db.Products.AddRange(p1, p2, p3);
        await db.SaveChangesAsync();

        var handler = new GetAdminProductsQueryHandler(db);

        // Act - Query all
        var resultAll = await handler.Handle(new GetAdminProductsQuery(PageNumber: 1, PageSize: 10), CancellationToken.None);
        resultAll.Should().HaveCount(3);

        // Act - Filter by Status Active
        var resultActive = await handler.Handle(new GetAdminProductsQuery(PageNumber: 1, PageSize: 10, Status: "Active"), CancellationToken.None);
        resultActive.Should().HaveCount(2);

        // Act - Filter by Organization1
        var resultOrg1 = await handler.Handle(new GetAdminProductsQuery(PageNumber: 1, PageSize: 10, OrganizationId: org1.Id), CancellationToken.None);
        resultOrg1.Should().HaveCount(2);
        resultOrg1.All(p => p.StoreName == "Store Alpha").Should().BeTrue();
    }
}
