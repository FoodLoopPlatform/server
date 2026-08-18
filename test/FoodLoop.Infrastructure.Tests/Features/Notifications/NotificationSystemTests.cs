using FluentAssertions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Notifications;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Application.Features.Notifications.Commands;
using FoodLoop.Application.Features.Orders.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Admin.Commands;
using FoodLoop.Infrastructure.Features.Notifications.Commands;
using FoodLoop.Infrastructure.Features.Orders.Commands;
using FoodLoop.Infrastructure.Hubs;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Services;
using FoodLoop.Infrastructure.Tests.TestSupport;
using FoodLoop.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FoodLoop.API.Controllers;

namespace FoodLoop.Infrastructure.Tests.Features.Notifications;

public class NotificationSystemTests : IDisposable
{
    private readonly ApplicationDbContext _db = ApplicationDbContextFactory.Create();
    private readonly Guid _userId = Guid.NewGuid();

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public void Hub_connection_should_require_authentication()
    {
        // Assert
        var hubType = typeof(NotificationHub);
        var authAttribute = hubType.GetCustomAttribute<AuthorizeAttribute>();
        authAttribute.Should().NotBeNull();
    }

    [Fact]
    public void Device_token_registration_endpoint_should_require_authentication()
    {
        // Assert
        var controllerType = typeof(NotificationsController);
        var authAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        authAttribute.Should().NotBeNull();
    }

    [Fact]
    public async Task Device_token_registration_should_deactivate_duplicates_for_other_users()
    {
        // Arrange
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();
        var commonToken = "device-token-12345";

        var tokenService = new UserDeviceTokenService(_db);

        // Act - Register for User A
        await tokenService.UpsertAsync(userAId, commonToken, "Android", CancellationToken.None);

        // Verify active for User A
        var tokensA = await tokenService.GetActiveTokensAsync(userAId, CancellationToken.None);
        tokensA.Should().ContainSingle().Which.Should().Be(commonToken);

        // Act - Register same token for User B
        await tokenService.UpsertAsync(userBId, commonToken, "iOS", CancellationToken.None);

        // Assert - User A should be deactivated
        var activeTokensA = _db.UserDeviceTokens
            .FirstOrDefault(t => t.UserId == userAId && t.Token == commonToken);
        activeTokensA.Should().NotBeNull();
        activeTokensA!.IsActive.Should().BeFalse();

        // Assert - User B should be active
        var activeTokensB = _db.UserDeviceTokens
            .FirstOrDefault(t => t.UserId == userBId && t.Token == commonToken);
        activeTokensB.Should().NotBeNull();
        activeTokensB!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Notification_failures_in_signalr_or_firebase_should_not_block_parent_transaction()
    {
        // Arrange
        var mockHubContext = new Mock<IHubContext<NotificationHub, INotificationHubClient>>();
        var mockClients = new Mock<IHubClients<INotificationHubClient>>();
        
        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        // Setup clients to throw an exception
        mockClients.Setup(c => c.User(It.IsAny<string>())).Throws(new Exception("SignalR connection failed"));

        var mockFirebase = new Mock<IFirebasePushNotificationService>();
        mockFirebase.Setup(f => f.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Throws(new Exception("Firebase gateway timeout"));

        var mockLogger = new Mock<ILogger<RealTimeNotificationService>>();
        var service = new RealTimeNotificationService(_db, mockHubContext.Object, mockFirebase.Object, mockLogger.Object);

        // Act & Assert
        // Calling this should NOT throw an exception despite SignalR and Firebase failing!
        Func<Task> act = async () => await service.SendNotificationToUserAsync(_userId, "Test Failure Isolation", "This is fine", "Test", CancellationToken.None);
        await act.Should().NotThrowAsync();

        // Ensure database state is still saved
        var inDb = _db.Notifications.Any(n => n.UserId == _userId && n.Title == "Test Failure Isolation");
        inDb.Should().BeTrue();
    }

    [Fact]
    public async Task SendNotification_to_offline_user_should_not_throw_exception()
    {
        // Arrange
        var mockHubContext = new Mock<IHubContext<NotificationHub, INotificationHubClient>>();
        var mockClients = new Mock<IHubClients<INotificationHubClient>>();
        var mockClientProxy = new Mock<INotificationHubClient>();

        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        // Returns a proxy representing a disconnected user connection
        mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(mockClientProxy.Object);

        var mockFirebase = new Mock<IFirebasePushNotificationService>();
        var mockLogger = new Mock<ILogger<RealTimeNotificationService>>();
        var service = new RealTimeNotificationService(_db, mockHubContext.Object, mockFirebase.Object, mockLogger.Object);

        // Act & Assert
        Func<Task> act = async () => await service.SendNotificationToUserAsync(_userId, "Test Offline User", "SignalR is fire-and-forget", "Test", CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReplyToSupportTicket_should_dispatch_exactly_one_notification_on_success()
    {
        // Arrange
        var ticket = new SupportTicket
        {
            UserId = _userId,
            Category = "Billing",
            Priority = TicketPriority.Normal,
            Status = TicketStatus.Open
        };
        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync();

        var mockNotification = new Mock<IRealTimeNotificationService>();
        var handler = new ReplyToSupportTicketCommandHandler(_db, mockNotification.Object);

        var command = new ReplyToSupportTicketCommand(ticket.Id, Guid.NewGuid(), "Support reply body", null);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert - Verify single dispatch
        mockNotification.Verify(n => n.SendNotificationToUserAsync(
            _userId,
            "Support Ticket Reply",
            It.Is<string>(s => s.Contains("Billing")),
            "SupportTicketReply",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAdminNote_should_dispatch_exactly_one_notification_if_not_internal()
    {
        // Arrange
        var admin = new ApplicationUser { Id = Guid.NewGuid(), UserName = "admin@example.com", FullName = "Admin User" };
        var recipient = new ApplicationUser { Id = _userId, UserName = "user@example.com", FullName = "Recipient User" };
        _db.Users.AddRange(admin, recipient);
        await _db.SaveChangesAsync();

        // UserManager setup
        var mockUserManager = MockUserManagerFactory.Create();
        mockUserManager.Setup(m => m.FindByIdAsync(admin.Id.ToString())).ReturnsAsync(admin);
        mockUserManager.Setup(m => m.FindByIdAsync(recipient.Id.ToString())).ReturnsAsync(recipient);

        var mockNotification = new Mock<IRealTimeNotificationService>();
        var mockAudit = new Mock<IAuditLogService>();
        var handler = new SendAdminNoteCommandHandler(_db, mockUserManager.Object, mockNotification.Object, mockAudit.Object);

        var command = new SendAdminNoteCommand(admin.Id, recipient.Id, "Warning", null, "Warning Note", "This is a warning note.", false);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert - Verify single dispatch
        mockNotification.Verify(n => n.SendNotificationToUserAsync(
            recipient.Id,
            "Warning Note",
            "This is a warning note.",
            "AdminWarning",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAdminNote_should_skip_notification_if_internal()
    {
        // Arrange
        var admin = new ApplicationUser { Id = Guid.NewGuid(), UserName = "admin@example.com", FullName = "Admin User" };
        var recipient = new ApplicationUser { Id = _userId, UserName = "user@example.com", FullName = "Recipient User" };
        _db.Users.AddRange(admin, recipient);
        await _db.SaveChangesAsync();

        var mockUserManager = MockUserManagerFactory.Create();
        mockUserManager.Setup(m => m.FindByIdAsync(admin.Id.ToString())).ReturnsAsync(admin);
        mockUserManager.Setup(m => m.FindByIdAsync(recipient.Id.ToString())).ReturnsAsync(recipient);

        var mockNotification = new Mock<IRealTimeNotificationService>();
        var mockAudit = new Mock<IAuditLogService>();
        var handler = new SendAdminNoteCommandHandler(_db, mockUserManager.Object, mockNotification.Object, mockAudit.Object);

        var command = new SendAdminNoteCommand(admin.Id, recipient.Id, "Notice", null, "Internal Note", "This is internal.", true);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert - Verify no dispatch
        mockNotification.Verify(n => n.SendNotificationToUserAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
