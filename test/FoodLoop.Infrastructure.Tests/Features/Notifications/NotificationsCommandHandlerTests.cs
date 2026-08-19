using FluentAssertions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Notifications;
using FoodLoop.Application.Features.Notifications.Commands;
using FoodLoop.Application.Features.Notifications.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Features.Notifications.Commands;
using FoodLoop.Infrastructure.Features.Notifications.Queries;
using FoodLoop.Infrastructure.Hubs;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Services;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Notifications;

public class NotificationsCommandHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _db = ApplicationDbContextFactory.Create();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _notificationId1 = Guid.NewGuid();
    private readonly Guid _notificationId2 = Guid.NewGuid();

    public NotificationsCommandHandlerTests()
    {
        // Seed notifications
        var n1 = new Notification
        {
            Id = _notificationId1,
            UserId = _userId,
            Title = "Alert 1",
            Body = "Welcome to FoodLoop",
            Type = "Welcome",
            IsRead = false
        };

        var n2 = new Notification
        {
            Id = _notificationId2,
            UserId = _userId,
            Title = "Alert 2",
            Body = "Your order is ready",
            Type = "OrderReady",
            IsRead = false
        };

        _db.Notifications.AddRange(n1, n2);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetMyNotifications_should_retrieve_user_notifications()
    {
        // Arrange
        var handler = new GetMyNotificationsQueryHandler(_db);
        var query = new GetMyNotificationsQuery(_userId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Select(n => n.Title).Should().Contain(new[] { "Alert 1", "Alert 2" });
    }

    [Fact]
    public async Task GetNotificationById_should_retrieve_single_notification_details()
    {
        // Arrange
        var handler = new GetNotificationByIdQueryHandler(_db);
        var query = new GetNotificationByIdQuery(_userId, _notificationId1);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(_notificationId1);
        result.Title.Should().Be("Alert 1");
        result.Body.Should().Be("Welcome to FoodLoop");
    }

    [Fact]
    public async Task GetNotificationById_should_throw_NotFoundException_for_invalid_or_unauthorized_id()
    {
        // Arrange
        var handler = new GetNotificationByIdQueryHandler(_db);
        var query = new GetNotificationByIdQuery(_userId, Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<FoodLoop.Application.Common.Exceptions.NotFoundException>(
            () => handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task MarkNotificationRead_should_update_single_status()
    {
        // Arrange
        var handler = new MarkNotificationReadCommandHandler(_db);
        var command = new MarkNotificationReadCommand(_userId, _notificationId1);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var n = await _db.Notifications.FindAsync(_notificationId1);
        n!.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task GetUnreadNotificationsCount_should_return_correct_count()
    {
        // Arrange
        var handler = new GetUnreadNotificationsCountQueryHandler(_db);
        var query = new GetUnreadNotificationsCountQuery(_userId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public async Task GetMyNotifications_with_isRead_filter_should_filter_correctly()
    {
        // Arrange
        var n = await _db.Notifications.FindAsync(_notificationId1);
        n!.IsRead = true;
        await _db.SaveChangesAsync();

        var handler = new GetMyNotificationsQueryHandler(_db);

        // Act
        var readNotifications = await handler.Handle(new GetMyNotificationsQuery(_userId, 1, 10, true), CancellationToken.None);
        var unreadNotifications = await handler.Handle(new GetMyNotificationsQuery(_userId, 1, 10, false), CancellationToken.None);

        // Assert
        readNotifications.Should().HaveCount(1);
        readNotifications.First().Id.Should().Be(_notificationId1);

        unreadNotifications.Should().HaveCount(1);
        unreadNotifications.First().Id.Should().Be(_notificationId2);
    }

    [Fact]
    public async Task MarkNotificationRead_should_fail_when_user_does_not_own_notification()
    {
        // Arrange
        var handler = new MarkNotificationReadCommandHandler(_db);
        var differentUserId = Guid.NewGuid();
        var command = new MarkNotificationReadCommand(differentUserId, _notificationId1);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task RegisterDeviceToken_should_save_device_token_successfully()
    {
        // Arrange
        var user = new ApplicationUser { Id = _userId, UserName = "device@test.com", Email = "device@test.com" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var handler = new RegisterDeviceTokenCommandHandler(_db);
        var command = new RegisterDeviceTokenCommand(_userId, "sample-fcm-token-12345", "Android");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var updatedUser = await _db.Users.FindAsync(_userId);
        updatedUser.Should().NotBeNull();
        updatedUser!.DeviceToken.Should().Be("sample-fcm-token-12345");
    }

    [Fact]
    public async Task MarkAllRead_should_update_all_notifications()
    {
        // Arrange
        var handler = new MarkAllNotificationsReadCommandHandler(_db);
        var command = new MarkAllNotificationsReadCommand(_userId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var list = _db.Notifications.Where(n => n.UserId == _userId).ToList();
        list.All(n => n.IsRead).Should().BeTrue();
    }

    [Fact]
    public async Task SendNotification_should_save_to_database_and_push_to_signalr()
    {
        // Arrange
        var mockHubContext = new Mock<IHubContext<NotificationHub, INotificationHubClient>>();
        var mockClients = new Mock<IHubClients<INotificationHubClient>>();
        var mockClientProxy = new Mock<INotificationHubClient>();

        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(mockClientProxy.Object);

        var mockLoc = new Mock<ILocalizationService>();
        mockLoc.Setup(l => l[It.IsAny<string>()]).Returns<string>(k => k);
        mockLoc.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns<string, object[]>((k, a) => k);

        var service = new RealTimeNotificationService(
            _db, 
            mockHubContext.Object, 
            new Mock<IFirebasePushNotificationService>().Object, 
            mockLoc.Object,
            new Mock<ILogger<RealTimeNotificationService>>().Object);

        // Act
        await service.SendNotificationToUserAsync(_userId, "Realtime Test", "SignalR is working", "OrderPlaced", Array.Empty<object>(), CancellationToken.None);

        // Assert
        var inDb = _db.Notifications.Any(n => n.UserId == _userId && n.Title == "Realtime Test");
        inDb.Should().BeTrue();

        mockClientProxy.Verify(c => c.ReceiveNotification(It.Is<NotificationDto>(dto =>
            dto.Title == "Realtime Test" &&
            dto.Body == "SignalR is working" &&
            dto.Type == "OrderPlaced")), Times.Once);
    }
}
