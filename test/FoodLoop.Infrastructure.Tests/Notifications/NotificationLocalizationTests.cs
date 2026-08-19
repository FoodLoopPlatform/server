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

    [Theory]
    [InlineData("en", "NotifOrderReceivedTitle", "NotifOrderReceivedBody", new object[] { "1234", "John Doe" }, "New Order Received", "New order #1234 received from John Doe.")]
    [InlineData("ar", "NotifOrderReceivedTitle", "NotifOrderReceivedBody", new object[] { "1234", "جون دو" }, "طلب جديد مستلم", "تم استلام طلب جديد #1234 من جون دو.")]
    [InlineData("en", "NotifOrderConfirmedTitle", "NotifOrderConfirmedBody", new object[] { "1234" }, "Order Confirmed", "Your order #1234 has been confirmed by the store.")]
    [InlineData("ar", "NotifOrderConfirmedTitle", "NotifOrderConfirmedBody", new object[] { "1234" }, "تم تأكيد الطلب", "تم تأكيد طلبك #1234 من قبل المتجر.")]
    [InlineData("en", "NotifOrderReadyTitle", "NotifOrderReadyBody", new object[] { "1234" }, "Order Ready for Pickup", "Your order #1234 is now ready for pickup.")]
    [InlineData("ar", "NotifOrderReadyTitle", "NotifOrderReadyBody", new object[] { "1234" }, "الطلب جاهز للاستلام", "طلبك #1234 جاهز الآن للاستلام.")]
    [InlineData("en", "NotifOrderPickedUpTitle", "NotifOrderPickedUpBody", new object[] { "1234" }, "Order Picked Up", "Your order #1234 has been picked up.")]
    [InlineData("ar", "NotifOrderPickedUpTitle", "NotifOrderPickedUpBody", new object[] { "1234" }, "تم استلام الطلب", "تم استلام طلبك #1234 بنجاح.")]
    [InlineData("en", "NotifOrderCompletedTitle", "NotifOrderCompletedBody", new object[] { "1234" }, "Order Completed", "Your order #1234 is complete. Thank you for using FoodLoop!")]
    [InlineData("ar", "NotifOrderCompletedTitle", "NotifOrderCompletedBody", new object[] { "1234" }, "اكتمل الطلب", "اكتمل طلبك #1234. شكراً لاستخدامك فودلوب!")]
    [InlineData("en", "NotifOrderCancelledTitle", "NotifOrderCancelledBody", new object[] { "1234" }, "Order Cancelled", "Your order #1234 has been cancelled.")]
    [InlineData("ar", "NotifOrderCancelledTitle", "NotifOrderCancelledBody", new object[] { "1234" }, "تم إلغاء الطلب", "تم إلغاء طلبك #1234.")]
    [InlineData("en", "NotifProductModerationTitle", "NotifProductModerationBodyOcr", new object[] { "Milk", "Bakery" }, "Product Requires Moderation", "New product 'Milk' from 'Bakery' requires moderation.")]
    [InlineData("ar", "NotifProductModerationTitle", "NotifProductModerationBodyOcr", new object[] { "حليب", "مخبز" }, "منتج يتطلب مراجعة", "المنتج الجديد 'حليب' من 'مخبز' يتطلب مراجعة الإدارة.")]
    [InlineData("en", "NotifProductReportedTitle", "NotifProductReportedBody", new object[] { "Bread", "Expired" }, "Product Reported", "Product 'Bread' was reported. Reason: Expired.")]
    [InlineData("ar", "NotifProductReportedTitle", "NotifProductReportedBody", new object[] { "خبز", "منتهي الصلاحية" }, "تم الإبلاغ عن منتج", "تم الإبلاغ عن المنتج 'خبز'. السبب: منتهي الصلاحية.")]
    [InlineData("en", "NotifSupportTicketCreatedTitle", "NotifSupportTicketCreatedBody", new object[] { "Payment", "Alice" }, "New Support Ticket", "New support ticket created in category 'Payment' by Alice.")]
    [InlineData("ar", "NotifSupportTicketCreatedTitle", "NotifSupportTicketCreatedBody", new object[] { "دفع", "أليس" }, "تذكرة دعم جديدة", "تم إنشاء تذكرة دعم جديدة في قسم 'دفع' بواسطة أليس.")]
    [InlineData("en", "NotifSupportTicketReplyTitle", "NotifSupportTicketReplyBody", new object[] { "Payment Issue" }, "Support Ticket Update", "There is a new update on your support ticket 'Payment Issue'.")]
    [InlineData("ar", "NotifSupportTicketReplyTitle", "NotifSupportTicketReplyBody", new object[] { "مشكلة دفع" }, "تحديث على تذكرة الدعم", "هناك رد جديد على تذكرة الدعم الخاصة بك 'مشكلة دفع'.")]
    [InlineData("en", "NotifNewUserRegisteredTitle", "NotifNewUserRegisteredBody", new object[] { "user@test.com", "Bob" }, "New User Registered", "A new user 'user@test.com' (Bob) has registered on the platform.")]
    [InlineData("ar", "NotifNewUserRegisteredTitle", "NotifNewUserRegisteredBody", new object[] { "user@test.com", "بوب" }, "تسجيل مستخدم جديد", "تم تسجيل مستخدم جديد 'user@test.com' (بوب) في المنصة.")]
    public async Task RealTimeNotificationService_should_correctly_localize_all_notification_keys(
        string lang, string titleKey, string bodyKey, object[] args, string expectedTitle, string expectedBody)
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"user_{Guid.NewGuid()}@test.com",
            Email = $"user_{Guid.NewGuid()}@test.com",
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
            titleKey,
            bodyKey,
            "GenericTest",
            args);

        // Assert
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.UserId == user.Id);
        notification.Should().NotBeNull();
        notification!.Title.Should().Be(expectedTitle);
        notification.Body.Should().Be(expectedBody);
    }

    [Fact]
    public async Task SendNotificationToRoleAsync_should_localize_per_admin_language()
    {
        // Arrange
        var adminEn = new ApplicationUser { Id = Guid.NewGuid(), UserName = "admin_en@test.com", FullName = "Admin EN", Language = "en" };
        var adminAr = new ApplicationUser { Id = Guid.NewGuid(), UserName = "admin_ar@test.com", FullName = "Admin AR", Language = "ar" };
        _db.Users.AddRange(adminEn, adminAr);
        await _db.SaveChangesAsync();

        var mockUserStore = new Mock<Microsoft.AspNetCore.Identity.IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>(mockUserStore.Object, null, null, null, null, null, null, null, null);
        mockUserManager.Setup(m => m.GetUsersInRoleAsync("Admin")).ReturnsAsync(new List<ApplicationUser> { adminEn, adminAr });

        var localizerFactory = new ResourceManagerStringLocalizerFactoryWrapper();
        var locService = new LocalizationService(localizerFactory);
        var mockHub = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<FoodLoop.Infrastructure.Hubs.NotificationHub, FoodLoop.Infrastructure.Hubs.INotificationHubClient>>();
        var mockFcm = new Mock<IFirebasePushNotificationService>();
        var mockLogger = new Mock<ILogger<RealTimeNotificationService>>();

        var service = new RealTimeNotificationService(_db, mockHub.Object, mockFcm.Object, mockUserManager.Object, locService, mockLogger.Object);

        // Act
        await service.SendNotificationToRoleAsync(
            "Admin",
            "NotifNewUserRegisteredTitle",
            "NotifNewUserRegisteredBody",
            "AccountCreated",
            new object[] { "newbie@test.com", "Newbie" });

        // Assert
        var notifEn = await _db.Notifications.FirstOrDefaultAsync(n => n.UserId == adminEn.Id);
        notifEn.Should().NotBeNull();
        notifEn!.Title.Should().Be("New User Registered");
        notifEn.Body.Should().Be("A new user 'newbie@test.com' (Newbie) has registered on the platform.");

        var notifAr = await _db.Notifications.FirstOrDefaultAsync(n => n.UserId == adminAr.Id);
        notifAr.Should().NotBeNull();
        notifAr!.Title.Should().Be("تسجيل مستخدم جديد");
        notifAr.Body.Should().Be("تم تسجيل مستخدم جديد 'newbie@test.com' (Newbie) في المنصة.");
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
