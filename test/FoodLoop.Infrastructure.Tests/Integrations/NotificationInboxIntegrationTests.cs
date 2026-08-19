using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Notifications;
using FoodLoop.Application.DTOs.Auth;
using FoodLoop.Application.Features.Auth.Commands;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Application.Features.SupportTickets.Commands;
using FoodLoop.Infrastructure.Features.Products.Commands;
using FoodLoop.Infrastructure.Features.SupportTickets.Commands;
using FoodLoop.Infrastructure.Features.Auth.Commands;
using FoodLoop.Infrastructure.Features.Users.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Hubs;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Integrations;

[Trait("Category", "Integration")]
public class NotificationInboxIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _dbOptions;

    public NotificationInboxIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using (var setupContext = new E2ETestApplicationDbContext(_dbOptions))
        {
            setupContext.Database.EnsureCreated();
        }
    }

    public void Dispose()
    {
        _connection.Close();
    }

    [Fact]
    public async Task NotificationHub_OnConnectedAsync_should_add_Admin_to_Admin_Group()
    {
        // Arrange
        var mockGroups = new Mock<IGroupManager>();
        var mockContext = new Mock<HubCallerContext>();
        var mockUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, "Admin")
        }));
        mockContext.Setup(c => c.User).Returns(mockUser);
        mockContext.Setup(c => c.ConnectionId).Returns("conn-id-123");

        var hub = new NotificationHub
        {
            Context = mockContext.Object,
            Groups = mockGroups.Object
        };

        // Act
        await hub.OnConnectedAsync();

        // Assert
        mockGroups.Verify(g => g.AddToGroupAsync("conn-id-123", "Admin", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotificationHub_OnDisconnectedAsync_should_remove_Admin_from_Admin_Group()
    {
        // Arrange
        var mockGroups = new Mock<IGroupManager>();
        var mockContext = new Mock<HubCallerContext>();
        var mockUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, "Admin")
        }));
        mockContext.Setup(c => c.User).Returns(mockUser);
        mockContext.Setup(c => c.ConnectionId).Returns("conn-id-123");

        var hub = new NotificationHub
        {
            Context = mockContext.Object,
            Groups = mockGroups.Object
        };

        // Act
        await hub.OnDisconnectedAsync(new Exception("disconnect"));

        // Assert
        mockGroups.Verify(g => g.RemoveFromGroupAsync("conn-id-123", "Admin", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that the EF Core model snapshot mappings for the three new columns 
    /// (ReadAt, EntityType, EntityId) correctly generate the expected schema and round-trip successfully in SQLite.
    /// Note: This is a schema mapping test using EnsureCreated, not an execution of migration scripts.
    /// </summary>
    [Fact]
    public void AddNotificationDeepLinkAndReadTracking_SchemaMapping_Should_Roundtrip_Correctly()
    {
        // Arrange
        var migrationConnection = new SqliteConnection("DataSource=:memory:");
        migrationConnection.Open();
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(migrationConnection)
                .Options;

            var testId = Guid.NewGuid();
            var recipientId = Guid.NewGuid();
            var readTime = DateTimeOffset.UtcNow;

            using (var context = new E2ETestApplicationDbContext(options))
            {
                context.Database.EnsureCreated();

                var notification = new Notification
                {
                    UserId = recipientId,
                    Title = "Migration Title",
                    Body = "Migration Body",
                    Type = "MigrationType",
                    IsRead = true,
                    ReadAt = readTime,
                    EntityType = "Product",
                    EntityId = testId
                };

                context.Notifications.Add(notification);
                context.SaveChanges();
            }

            // Act & Assert
            using (var context = new E2ETestApplicationDbContext(options))
            {
                var retrieved = context.Notifications.Single(n => n.UserId == recipientId);
                retrieved.Title.Should().Be("Migration Title");
                retrieved.IsRead.Should().BeTrue();
                retrieved.ReadAt.Should().BeCloseTo(readTime, TimeSpan.FromSeconds(1));
                retrieved.EntityType.Should().Be("Product");
                retrieved.EntityId.Should().Be(testId);
            }
        }
        finally
        {
            migrationConnection.Close();
        }
    }

    [Fact]
    public async Task SendNotificationToUserAsync_should_persist_and_dispatch_correctly()
    {
        // Arrange
        var db = new E2ETestApplicationDbContext(_dbOptions);
        var mockHub = new Mock<IHubContext<NotificationHub, INotificationHubClient>>();
        var mockClients = new Mock<IHubClients<INotificationHubClient>>();
        var mockClient = new Mock<INotificationHubClient>();

        mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(mockClient.Object);

        var mockFcm = new Mock<IFirebasePushNotificationService>();
        var mockLogger = new Mock<ILogger<RealTimeNotificationService>>();
        var mockUserStore = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(mockUserStore.Object, null, null, null, null, null, null, null, null);

        var service = new RealTimeNotificationService(db, mockHub.Object, mockFcm.Object, mockUserManager.Object, mockLogger.Object);
        var recipientId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        // Act
        await service.SendNotificationToUserAsync(recipientId, "Title", "Body", "OrderConfirmed", "Product", entityId);

        // Assert
        var dbRecord = db.Notifications.FirstOrDefault(n => n.UserId == recipientId);
        dbRecord.Should().NotBeNull();
        dbRecord!.Title.Should().Be("Title");
        dbRecord.EntityType.Should().Be("Product");
        dbRecord.EntityId.Should().Be(entityId);

        mockClient.Verify(c => c.ReceiveNotification(It.Is<NotificationDto>(dto => dto.EntityType == "Product" && dto.EntityId == entityId)), Times.Once);
        mockFcm.Verify(f => f.SendToUserAsync(recipientId, "Title", "Body", "OrderConfirmed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendNotificationToUserAsync_should_succeed_with_null_entity_parameters_for_existing_callsites()
    {
        // Arrange
        var db = new E2ETestApplicationDbContext(_dbOptions);
        var mockHub = new Mock<IHubContext<NotificationHub, INotificationHubClient>>();
        var mockClients = new Mock<IHubClients<INotificationHubClient>>();
        var mockClient = new Mock<INotificationHubClient>();

        mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(mockClient.Object);

        var mockFcm = new Mock<IFirebasePushNotificationService>();
        var mockLogger = new Mock<ILogger<RealTimeNotificationService>>();
        var mockUserStore = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(mockUserStore.Object, null, null, null, null, null, null, null, null);

        var service = new RealTimeNotificationService(db, mockHub.Object, mockFcm.Object, mockUserManager.Object, mockLogger.Object);
        var recipientId = Guid.NewGuid();

        // Act & Assert
        Func<Task> act = async () => await service.SendNotificationToUserAsync(recipientId, "Legacy Title", "Legacy Body", "LegacyType");
        await act.Should().NotThrowAsync();

        var dbRecord = db.Notifications.FirstOrDefault(n => n.UserId == recipientId);
        dbRecord.Should().NotBeNull();
        dbRecord!.Title.Should().Be("Legacy Title");
        dbRecord.EntityType.Should().BeNull();
        dbRecord.EntityId.Should().BeNull();
    }

    [Fact]
    public async Task SendNotificationToRoleAsync_should_complete_without_error_when_no_users_exist_in_target_role()
    {
        // Arrange
        var db = new E2ETestApplicationDbContext(_dbOptions);
        var mockHub = new Mock<IHubContext<NotificationHub, INotificationHubClient>>();
        var mockFcm = new Mock<IFirebasePushNotificationService>();
        var mockLogger = new Mock<ILogger<RealTimeNotificationService>>();

        var mockUserStore = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(mockUserStore.Object, null, null, null, null, null, null, null, null);
        mockUserManager.Setup(m => m.GetUsersInRoleAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<ApplicationUser>()); // No users in role

        var service = new RealTimeNotificationService(db, mockHub.Object, mockFcm.Object, mockUserManager.Object, mockLogger.Object);

        // Act & Assert
        Func<Task> act = async () => await service.SendNotificationToRoleAsync("Admin", "Title", "Body", "Event");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendNotificationToRoleAsync_should_isolate_failures_so_one_recipients_dispatch_failure_does_not_block_others()
    {
        // Arrange
        var db = new E2ETestApplicationDbContext(_dbOptions);
        var mockHub = new Mock<IHubContext<NotificationHub, INotificationHubClient>>();
        var mockClients = new Mock<IHubClients<INotificationHubClient>>();
        var mockClient = new Mock<INotificationHubClient>();

        mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(mockClient.Object);

        var mockFcm = new Mock<IFirebasePushNotificationService>();
        var mockLogger = new Mock<ILogger<RealTimeNotificationService>>();

        var admin1 = new ApplicationUser { Id = Guid.NewGuid(), UserName = "admin1@foodloop.com", Email = "admin1@foodloop.com" };
        var admin2 = new ApplicationUser { Id = Guid.NewGuid(), UserName = "admin2@foodloop.com", Email = "admin2@foodloop.com" };

        var mockUserStore = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(mockUserStore.Object, null, null, null, null, null, null, null, null);
        mockUserManager.Setup(m => m.GetUsersInRoleAsync("Admin"))
            .ReturnsAsync(new List<ApplicationUser> { admin1, admin2 });

        // Simulate delivery failure for admin1
        mockFcm.Setup(f => f.SendToUserAsync(admin1.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("FCM failure"));

        var service = new RealTimeNotificationService(db, mockHub.Object, mockFcm.Object, mockUserManager.Object, mockLogger.Object);

        // Act
        await service.SendNotificationToRoleAsync("Admin", "Role Title", "Role Body", "RoleEvent");

        // Assert
        db.Notifications.Count(n => n.UserId == admin1.Id).Should().Be(1);
        db.Notifications.Count(n => n.UserId == admin2.Id).Should().Be(1);

        mockFcm.Verify(f => f.SendToUserAsync(admin1.Id, "Role Title", "Role Body", "RoleEvent", It.IsAny<CancellationToken>()), Times.Once);
        mockFcm.Verify(f => f.SendToUserAsync(admin2.Id, "Role Title", "Role Body", "RoleEvent", It.IsAny<CancellationToken>()), Times.Once);

        // Confirm warnings were logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DELIVERY FAILED")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendNotificationToRoleAsync_should_log_Error_level_when_database_save_fails_for_a_recipient()
    {
        // Arrange
        var mockContext = new Mock<ApplicationDbContext>(new DbContextOptions<ApplicationDbContext>());
        var mockHub = new Mock<IHubContext<NotificationHub, INotificationHubClient>>();
        var mockFcm = new Mock<IFirebasePushNotificationService>();
        var mockLogger = new Mock<ILogger<RealTimeNotificationService>>();

        var admin1 = new ApplicationUser { Id = Guid.NewGuid(), UserName = "admin1@foodloop.com", Email = "admin1@foodloop.com" };
        var admin2 = new ApplicationUser { Id = Guid.NewGuid(), UserName = "admin2@foodloop.com", Email = "admin2@foodloop.com" };

        var mockUserStore = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(mockUserStore.Object, null, null, null, null, null, null, null, null);
        mockUserManager.Setup(m => m.GetUsersInRoleAsync("Admin"))
            .ReturnsAsync(new List<ApplicationUser> { admin1, admin2 });

        // Throw database exception on save changes
        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("DB write failure"));

        var service = new RealTimeNotificationService(mockContext.Object, mockHub.Object, mockFcm.Object, mockUserManager.Object, mockLogger.Object);

        // Act
        await service.SendNotificationToRoleAsync("Admin", "Role Title", "Role Body", "RoleEvent");

        // Assert - verify DB error was logged with correct format and next user was processed (loop not terminated)
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DATABASE SAVE FAILED") && v.ToString()!.Contains(admin1.Id.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DATABASE SAVE FAILED") && v.ToString()!.Contains(admin2.Id.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task NotificationHub_OnConnectedAsync_should_NOT_add_non_Admin_to_Admin_Group()
    {
        // Arrange
        var mockGroups = new Mock<IGroupManager>();
        var mockContext = new Mock<HubCallerContext>();
        
        // A customer role (non-admin)
        var mockUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, "Customer")
        }));
        mockContext.Setup(c => c.User).Returns(mockUser);
        mockContext.Setup(c => c.ConnectionId).Returns("conn-id-456");

        var hub = new NotificationHub
        {
            Context = mockContext.Object,
            Groups = mockGroups.Object
        };

        // Act
        await hub.OnConnectedAsync();

        // Assert
        mockGroups.Verify(g => g.AddToGroupAsync("conn-id-456", "Admin", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateProduct_should_notify_admins_when_product_is_pending_moderation()
    {
        // Arrange
        using var db = new E2ETestApplicationDbContext(_dbOptions);
        var unitOfWork = new UnitOfWork(db);
        var mockAudit = new Mock<IAuditLogService>();
        var mockNotification = new Mock<IRealTimeNotificationService>();

        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var catId = Guid.NewGuid();

        var organization = new Organization
        {
            Id = orgId,
            OwnerId = ownerId,
            Name = "Test Org",
            VerificationStatus = VerificationStatus.Verified
        };
        db.Organizations.Add(organization);

        var category = new Category
        {
            Id = catId,
            Name = "Fruit"
        };
        db.Categories.Add(category);
        db.SaveChanges();

        var handler = new CreateProductCommandHandler(unitOfWork, mockAudit.Object, mockNotification.Object);

        var command = new CreateProductCommand(
            OwnerId: ownerId,
            CategoryId: catId,
            Title: "Expired Milk",
            Description: "Old",
            OriginalPrice: 5.0m,
            DiscountedPrice: 2.0m,
            QuantityAvailable: 1,
            ExpirationDate: DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            ExpiryVerificationState: ExpiryVerificationState.AiLowConfidence
        );

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        mockNotification.Verify(
            n => n.SendNotificationToRoleAsync(
                "Admin",
                "Product Requires Moderation",
                It.Is<string>(s => s.Contains("Expired Milk")),
                "ProductUploaded",
                "Product",
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateProduct_should_not_notify_admins_when_product_is_not_pending_moderation()
    {
        // Arrange
        using var db = new E2ETestApplicationDbContext(_dbOptions);
        var unitOfWork = new UnitOfWork(db);
        var mockAudit = new Mock<IAuditLogService>();
        var mockNotification = new Mock<IRealTimeNotificationService>();

        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var catId = Guid.NewGuid();

        var organization = new Organization
        {
            Id = orgId,
            OwnerId = ownerId,
            Name = "Test Org",
            VerificationStatus = VerificationStatus.Verified
        };
        db.Organizations.Add(organization);

        var category = new Category
        {
            Id = catId,
            Name = "Fruit"
        };
        db.Categories.Add(category);
        db.SaveChanges();

        var handler = new CreateProductCommandHandler(unitOfWork, mockAudit.Object, mockNotification.Object);

        var command = new CreateProductCommand(
            OwnerId: ownerId,
            CategoryId: catId,
            Title: "Fresh Milk",
            Description: "Fresh",
            OriginalPrice: 5.0m,
            DiscountedPrice: 2.0m,
            QuantityAvailable: 1,
            ExpirationDate: DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            ExpiryVerificationState: ExpiryVerificationState.AiVerified
        );

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        mockNotification.Verify(
            n => n.SendNotificationToRoleAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateSupportTicket_should_notify_admins()
    {
        // Arrange
        using var db = new E2ETestApplicationDbContext(_dbOptions);
        var mockAudit = new Mock<IAuditLogService>();
        var mockNotification = new Mock<IRealTimeNotificationService>();

        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, FullName = "Support User", Email = "support@example.com" };
        db.Users.Add(user);
        db.SaveChanges();

        var handler = new CreateSupportTicketCommandHandler(db, mockAudit.Object, mockNotification.Object);
        var command = new CreateSupportTicketCommand(userId, "TechnicalIssue", "Need help!", TicketPriority.High);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        mockNotification.Verify(
            n => n.SendNotificationToRoleAsync(
                "Admin",
                "New Support Ticket",
                It.Is<string>(s => s.Contains("TechnicalIssue") && s.Contains("Support User")),
                "SupportTicketCreated",
                "SupportTicket",
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReportProduct_should_notify_admins()
    {
        // Arrange
        using var db = new E2ETestApplicationDbContext(_dbOptions);
        var mockAudit = new Mock<IAuditLogService>();
        var mockNotification = new Mock<IRealTimeNotificationService>();

        var reporterId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var catId = Guid.NewGuid();

        var owner = new ApplicationUser { Id = ownerId, FullName = "Owner User", Email = "owner@example.com" };
        db.Users.Add(owner);

        var category = new Category { Id = catId, Name = "Fruit" };
        db.Categories.Add(category);

        var product = new Product { Id = productId, Title = "Bad Apples", OrganizationId = orgId, CategoryId = catId };
        var org = new Organization { Id = orgId, OwnerId = ownerId, Name = "Merchant Org" };

        db.Products.Add(product);
        db.Organizations.Add(org);
        db.SaveChanges();

        var handler = new ReportProductCommandHandler(db, mockAudit.Object, mockNotification.Object);
        var command = new ReportProductCommand(reporterId, productId, "Expired", "Apples are rotten.");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        mockNotification.Verify(
            n => n.SendNotificationToRoleAsync(
                "Admin",
                "Product Reported",
                It.Is<string>(s => s.Contains("Bad Apples")),
                "ProductReported",
                "ProductReport",
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RegisterUser_should_notify_admins()
    {
        // Arrange
        using var db = new E2ETestApplicationDbContext(_dbOptions);
        var unitOfWork = new UnitOfWork(db);
        var mockUserStore = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(mockUserStore.Object, null, null, null, null, null, null, null, null);
        var mockEmail = new Mock<IEmailService>();
        var mockLoc = new Mock<ILocalizationService>();
        var mockAudit = new Mock<IAuditLogService>();
        var mockNotification = new Mock<IRealTimeNotificationService>();

        mockUserManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        mockUserManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var request = new RegisterRequest
        {
            Name = "Register User",
            Email = "register@example.com",
            Password = "Password123!",
            Role = AppRole.Customer
        };
        var command = new RegisterCommand(request, "127.0.0.1");

        var handler = new RegisterCommandHandler(mockUserManager.Object, unitOfWork, mockEmail.Object, mockLoc.Object, mockAudit.Object, mockNotification.Object);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        mockNotification.Verify(
            n => n.SendNotificationToRoleAsync(
                "Admin",
                "New User Registered",
                It.Is<string>(s => s.Contains("register@example.com")),
                "AccountCreated",
                "User",
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
