using FluentAssertions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Orders;
using FoodLoop.Application.Features.Orders.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Orders.Commands;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Notifications;

public class NotificationLocalizationTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly DbContextOptions<ApplicationDbContext> _dbOptions;

    public NotificationLocalizationTests()
    {
        _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"NotifLocDb_{Guid.NewGuid()}")
            .Options;
        _db = new ApplicationDbContext(_dbOptions);
    }

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData("en", "Order Placed Successfully", "Your order #12345678 has been placed successfully.")]
    [InlineData("ar", "تم تقديم الطلب بنجاح", "تم تقديم طلبك #12345678 بنجاح.")]
    public async Task RealTimeNotificationService_should_localize_strings_at_write_time_based_on_user_language(
        string lang, string expectedTitle, string expectedBody)
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"user_{lang}@test.com",
            Email = $"user_{lang}@test.com",
            Language = lang
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var localizerFactory = new ResourceManagerStringLocalizerFactoryWrapper();
        var locService = new LocalizationService(localizerFactory);
        var mockHub = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<FoodLoop.Infrastructure.Hubs.NotificationHub, FoodLoop.Infrastructure.Hubs.INotificationHubClient>>();
        var mockFcm = new Mock<IFirebasePushNotificationService>();
        var mockLogger = new Mock<ILogger<RealTimeNotificationService>>();

        var service = new RealTimeNotificationService(_db, mockHub.Object, mockFcm.Object, locService, mockLogger.Object);

        // Act
        await service.SendNotificationToUserAsync(
            user.Id,
            "NotifOrderPlacedTitle",
            "NotifOrderPlacedBody",
            "OrderPlaced",
            new object[] { "12345678" });

        // Assert
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.UserId == user.Id);
        notification.Should().NotBeNull();
        notification!.Title.Should().Be(expectedTitle);
        notification.Body.Should().Be(expectedBody);
    }

    [Fact]
    public async Task UpdateOrderStatus_Pending_should_map_to_NotifOrderPending_keys()
    {
        // Arrange
        var customer = new ApplicationUser { Id = Guid.NewGuid(), UserName = "cust@test.com", FullName = "Cust User" };
        var merchant = new ApplicationUser { Id = Guid.NewGuid(), UserName = "merch@test.com", FullName = "Merch User" };
        _db.Users.AddRange(customer, merchant);

        var org = new Organization { Id = Guid.NewGuid(), OwnerId = merchant.Id, Name = "Pending Org", VerificationStatus = VerificationStatus.Verified };
        _db.Organizations.Add(org);

        var cat = new Category { Id = Guid.NewGuid(), Name = "General" };
        _db.Categories.Add(cat);

        var product = new Product { Id = Guid.NewGuid(), OrganizationId = org.Id, CategoryId = cat.Id, Title = "Item", OriginalPrice = 10m, DiscountedPrice = 8m, QuantityAvailable = 1, ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)), Status = ProductStatus.Active };
        _db.Products.Add(product);

        var order = new Order { UserId = customer.Id, OrderStatus = OrderStatus.Confirmed, PaymentStatus = PaymentStatus.Pending, TotalAmount = 8m };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 1, UnitPrice = 8m, Product = product });
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var mockNotification = new Mock<IRealTimeNotificationService>();
        var mockAudit = new Mock<IAuditLogService>();
        var handler = new UpdateOrderStatusCommandHandler(_db, mockAudit.Object, mockNotification.Object);

        var command = new UpdateOrderStatusCommand(merchant.Id, order.Id, "Pending");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        mockNotification.Verify(n => n.SendNotificationToUserAsync(
            customer.Id,
            "NotifOrderPendingTitle",
            "NotifOrderPendingBody",
            "OrderPending",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class ResourceManagerStringLocalizerFactoryWrapper : IStringLocalizerFactory
{
    public IStringLocalizer Create(Type resourceSource)
    {
        return new ResourceManagerStringLocalizer(
            new System.Resources.ResourceManager("FoodLoop.Infrastructure.Resources.FoodLoop.Infrastructure.Resources.Messages", typeof(FoodLoop.Infrastructure.Resources.Messages).Assembly),
            typeof(FoodLoop.Infrastructure.Resources.Messages).Assembly,
            "FoodLoop.Infrastructure.Resources.FoodLoop.Infrastructure.Resources.Messages",
            new ResourceNamesCache(),
            new Mock<ILogger<ResourceManagerStringLocalizer>>().Object);
    }

    public IStringLocalizer Create(string baseName, string location)
    {
        var asm = typeof(FoodLoop.Infrastructure.Resources.Messages).Assembly;
        return new ResourceManagerStringLocalizer(
            new System.Resources.ResourceManager(baseName, asm),
            asm,
            baseName,
            new ResourceNamesCache(),
            new Mock<ILogger<ResourceManagerStringLocalizer>>().Object);
    }
}
