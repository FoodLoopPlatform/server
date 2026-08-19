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
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FoodLoop.API.Controllers;
using Google.Apis.Auth.OAuth2;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;

namespace FoodLoop.Infrastructure.Tests.Features.Notifications;

public class TestHttpClientFactory : Google.Apis.Http.HttpClientFactory
{
    private readonly HttpMessageHandler _handler;
    public TestHttpClientFactory(HttpMessageHandler handler)
    {
        _handler = handler;
    }
    protected override HttpMessageHandler CreateHandler(Google.Apis.Http.CreateHttpClientArgs args)
    {
        return _handler;
    }
}

public class TestableFirebasePushNotificationService : FirebasePushNotificationService
{
    public Func<Message, CancellationToken, Task<string>> SendMessageHook { get; set; }

    public TestableFirebasePushNotificationService(
        ApplicationDbContext db, 
        Microsoft.Extensions.Options.IOptions<FoodLoop.Infrastructure.Options.FirebaseOptions> options, 
        ILogger<FirebasePushNotificationService> logger) 
        : base(db, options, logger)
    {
    }

    protected override Task<string> SendMessageAsync(Message message, CancellationToken cancellationToken)
    {
        if (SendMessageHook != null)
        {
            return SendMessageHook(message, cancellationToken);
        }
        return base.SendMessageAsync(message, cancellationToken);
    }
}

public class NotificationSystemTests : IDisposable
{
    private readonly ApplicationDbContext _db = ApplicationDbContextFactory.Create();
    private readonly Guid _userId = Guid.NewGuid();

    private static readonly object _lock = new();
    private static Mock<HttpMessageHandler> _mockHttpHandler;

    static NotificationSystemTests()
    {
        lock (_lock)
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                string privateKeyPem;
                using (var rsa = System.Security.Cryptography.RSA.Create(2048))
                {
                    var pkcs8Bytes = rsa.ExportPkcs8PrivateKey();
                    var privateKeyBase64 = Convert.ToBase64String(pkcs8Bytes);
                    privateKeyPem = $"-----BEGIN PRIVATE KEY-----\\n{privateKeyBase64}\\n-----END PRIVATE KEY-----\\n";
                }

                var dummyJson = $@"
                {{
                  ""type"": ""service_account"",
                  ""project_id"": ""dummy-project"",
                  ""private_key_id"": ""dummy-key-id"",
                  ""private_key"": ""{privateKeyPem}"",
                  ""client_email"": ""dummy@dummy.iam.gserviceaccount.com""
                }}";

                _mockHttpHandler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
                
                // Set up the token endpoint response so authentication doesn't fail
                _mockHttpHandler
                    .Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.AbsoluteUri.Contains("oauth2.googleapis.com")),
                        ItExpr.IsAny<CancellationToken>()
                    )
                    .ReturnsAsync(new HttpResponseMessage
                    {
                        StatusCode = System.Net.HttpStatusCode.OK,
                        Content = new StringContent(
                            "{\"access_token\":\"dummy_token\",\"expires_in\":3600,\"token_type\":\"Bearer\"}",
                            System.Text.Encoding.UTF8,
                            "application/json")
                    });

                var credential = GoogleCredential.FromJson(dummyJson);
                FirebaseApp.Create(new AppOptions
                {
                    Credential = credential,
                    ProjectId = "dummy-project",
                    HttpClientFactory = new TestHttpClientFactory(_mockHttpHandler.Object)
                });
            }
        }
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    private FirebaseMessagingException CreateFirebaseException(MessagingErrorCode messagingErrorCode, string messageText = "unregistered token test")
    {
        var ctor = typeof(FirebaseMessagingException)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault();

        if (ctor == null)
        {
            throw new InvalidOperationException("FirebaseMessagingException has no constructor.");
        }

        // Map MessagingErrorCode to ErrorCode
        ErrorCode errorCode = ErrorCode.Internal;
        if (messagingErrorCode == MessagingErrorCode.InvalidArgument)
        {
            errorCode = ErrorCode.InvalidArgument;
        }
        else if (messagingErrorCode == MessagingErrorCode.Unregistered)
        {
            errorCode = ErrorCode.NotFound;
        }

        var parameters = ctor.GetParameters();
        var args = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var pType = parameters[i].ParameterType;
            if (pType == typeof(string))
            {
                args[i] = messageText;
            }
            else if (pType == typeof(ErrorCode))
            {
                args[i] = errorCode;
            }
            else if (pType == typeof(MessagingErrorCode) || pType == typeof(MessagingErrorCode?))
            {
                args[i] = messagingErrorCode;
            }
            else
            {
                args[i] = null;
            }
        }

        return (FirebaseMessagingException)ctor.Invoke(args);
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
    public async Task SendToUser_should_deactivate_token_when_fcm_returns_unregistered()
    {
        // Arrange
        var user = new ApplicationUser { Id = _userId, UserName = "fcm-unreg@example.com", Email = "fcm-unreg@example.com", FullName = "FCM Unregistered User" };
        _db.Users.Add(user);
        
        var token = "token-unreg-123";
        _db.UserDeviceTokens.Add(new UserDeviceToken
        {
            UserId = _userId,
            Token = token,
            IsActive = true
        });
        await _db.SaveChangesAsync();

        var options = Microsoft.Extensions.Options.Options.Create(new FoodLoop.Infrastructure.Options.FirebaseOptions
        {
            Enabled = true,
            ProjectId = "dummy-project",
            ServiceAccountJson = "dummy"
        });
        var logger = new Mock<ILogger<FirebasePushNotificationService>>();
        var service = new TestableFirebasePushNotificationService(_db, options, logger.Object);

        // Setup hook to throw Unregistered
        service.SendMessageHook = (msg, cancel) =>
        {
            throw CreateFirebaseException(MessagingErrorCode.Unregistered, "The token is unregistered.");
        };

        // Act
        await service.SendToUserAsync(_userId, "Test Title", "Test Body", "TestType", CancellationToken.None);

        // Assert
        var dbToken = _db.UserDeviceTokens.FirstOrDefault(t => t.UserId == _userId && t.Token == token);
        dbToken.Should().NotBeNull();
        dbToken!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SendToUser_should_deactivate_token_when_fcm_returns_invalid_argument()
    {
        // Arrange
        var user = new ApplicationUser { Id = _userId, UserName = "fcm-invalid@example.com", Email = "fcm-invalid@example.com", FullName = "FCM Invalid User" };
        _db.Users.Add(user);
        
        var token = "token-invalid-123";
        _db.UserDeviceTokens.Add(new UserDeviceToken
        {
            UserId = _userId,
            Token = token,
            IsActive = true
        });
        await _db.SaveChangesAsync();

        var options = Microsoft.Extensions.Options.Options.Create(new FoodLoop.Infrastructure.Options.FirebaseOptions
        {
            Enabled = true,
            ProjectId = "dummy-project",
            ServiceAccountJson = "dummy"
        });
        var logger = new Mock<ILogger<FirebasePushNotificationService>>();
        var service = new TestableFirebasePushNotificationService(_db, options, logger.Object);

        // Setup hook to throw InvalidArgument
        service.SendMessageHook = (msg, cancel) =>
        {
            throw CreateFirebaseException(MessagingErrorCode.InvalidArgument, "The token format is invalid.");
        };

        // Act
        await service.SendToUserAsync(_userId, "Test Title", "Test Body", "TestType", CancellationToken.None);

        // Assert
        var dbToken = _db.UserDeviceTokens.FirstOrDefault(t => t.UserId == _userId && t.Token == token);
        dbToken.Should().NotBeNull();
        dbToken!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SendToUser_should_not_deactivate_token_when_fcm_returns_internal_error()
    {
        // Arrange
        var user = new ApplicationUser { Id = _userId, UserName = "fcm-internal@example.com", Email = "fcm-internal@example.com", FullName = "FCM Internal Error User" };
        _db.Users.Add(user);
        
        var token = "token-internal-123";
        _db.UserDeviceTokens.Add(new UserDeviceToken
        {
            UserId = _userId,
            Token = token,
            IsActive = true
        });
        await _db.SaveChangesAsync();

        var options = Microsoft.Extensions.Options.Options.Create(new FoodLoop.Infrastructure.Options.FirebaseOptions
        {
            Enabled = true,
            ProjectId = "dummy-project",
            ServiceAccountJson = "dummy"
        });
        var logger = new Mock<ILogger<FirebasePushNotificationService>>();
        var service = new TestableFirebasePushNotificationService(_db, options, logger.Object);

        // Setup hook to throw Internal (non-stale error)
        service.SendMessageHook = (msg, cancel) =>
        {
            throw CreateFirebaseException(MessagingErrorCode.Internal, "Internal server error occurred.");
        };

        // Act
        await service.SendToUserAsync(_userId, "Test Title", "Test Body", "TestType", CancellationToken.None);

        // Assert
        var dbToken = _db.UserDeviceTokens.FirstOrDefault(t => t.UserId == _userId && t.Token == token);
        dbToken.Should().NotBeNull();
        dbToken!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Notification_failures_in_signalr_or_firebase_should_not_block_parent_transaction()
    {
        // Arrange
        var mockHubContext = new Mock<IHubContext<NotificationHub, INotificationHubClient>>();
        var mockClients = new Mock<IHubClients<INotificationHubClient>>();
        
        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.User(It.IsAny<string>())).Throws(new Exception("SignalR connection failed"));

        var mockFirebase = new Mock<IFirebasePushNotificationService>();
        mockFirebase.Setup(f => f.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Throws(new Exception("Firebase gateway timeout"));

        var mockLoc = new Mock<ILocalizationService>();
        mockLoc.Setup(l => l[It.IsAny<string>()]).Returns<string>(k => k);
        mockLoc.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns<string, object[]>((k, a) => k);

        var mockLogger = new Mock<ILogger<RealTimeNotificationService>>();
        var service = new RealTimeNotificationService(_db, mockHubContext.Object, mockFirebase.Object, mockLoc.Object, mockLogger.Object);

        // Act & Assert
        Func<Task> act = async () => await service.SendNotificationToUserAsync(_userId, "Test Failure Isolation", "This is fine", "Test", Array.Empty<object>(), CancellationToken.None);
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
        mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(mockClientProxy.Object);

        var mockFirebase = new Mock<IFirebasePushNotificationService>();
        var mockLogger = new Mock<ILogger<RealTimeNotificationService>>();
        var mockLoc = new Mock<ILocalizationService>();
        mockLoc.Setup(l => l[It.IsAny<string>()]).Returns<string>(k => k);
        mockLoc.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns<string, object[]>((k, a) => k);

        var service = new RealTimeNotificationService(_db, mockHubContext.Object, mockFirebase.Object, mockLoc.Object, mockLogger.Object);

        // Act & Assert
        Func<Task> act = async () => await service.SendNotificationToUserAsync(_userId, "Test Offline User", "SignalR is fire-and-forget", "Test", Array.Empty<object>(), CancellationToken.None);
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
            "NotifSupportTicketReplyTitle",
            "NotifSupportTicketReplyBody",
            "SupportTicketReply",
            It.Is<object[]>(args => args.Length == 1 && (string)args[0] == "Billing"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("Warning", "AdminWarning")]
    [InlineData("Urgent", "AdminUrgent")]
    [InlineData("Notice", "AdminNotice")]
    public async Task SendAdminNote_should_dispatch_exactly_one_notification_of_correct_type_if_not_internal(string category, string expectedNotificationType)
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

        var command = new SendAdminNoteCommand(admin.Id, recipient.Id, category, null, "Warning Note", "This is a warning note.", false);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert - Verify single dispatch
        mockNotification.Verify(n => n.SendNotificationToUserAsync(
            recipient.Id,
            "Warning Note",
            "This is a warning note.",
            expectedNotificationType,
            It.IsAny<object[]>(),
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
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrder_should_dispatch_exactly_one_customer_and_one_merchant_notification()
    {
        // Arrange
        var customer = new ApplicationUser { Id = _userId, UserName = "cust@example.com", Email = "cust@example.com", FullName = "Customer Name", Status = UserStatus.Active };
        var merchant = new ApplicationUser { Id = Guid.NewGuid(), UserName = "merch@example.com", Email = "merch@example.com", FullName = "Merchant Name", Status = UserStatus.Active };
        _db.Users.AddRange(customer, merchant);

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = merchant.Id,
            Name = "Order Bakery",
            VerificationStatus = VerificationStatus.Verified
        };
        _db.Organizations.Add(org);

        var category = new Category { Id = Guid.NewGuid(), Name = "Bakery" };
        _db.Categories.Add(category);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            CategoryId = category.Id,
            Title = "Fresh Croissant",
            OriginalPrice = 12.0m,
            DiscountedPrice = 10.0m,
            QuantityAvailable = 10,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            Status = ProductStatus.Active
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var mockNotification = new Mock<IRealTimeNotificationService>();
        var mockAudit = new Mock<IAuditLogService>();
        
        var handler = new CreateOrderCommandHandler(_db, mockAudit.Object, mockNotification.Object);

        var command = new CreateOrderCommand(
            UserId: customer.Id,
            Items: new List<CheckoutItemRequest> { new CheckoutItemRequest(product.Id, 2) },
            IpAddress: "127.0.0.1"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        // 1. Assert consumer notification is dispatched once
        mockNotification.Verify(n => n.SendNotificationToUserAsync(
            customer.Id,
            "NotifOrderPlacedTitle",
            "NotifOrderPlacedBody",
            "OrderPlaced",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // 2. Assert merchant notification is dispatched once
        mockNotification.Verify(n => n.SendNotificationToUserAsync(
            merchant.Id,
            "NotifOrderReceivedTitle",
            "NotifOrderReceivedBody",
            "OrderReceived",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
            
        // 3. Assert total dispatches is exactly 2
        mockNotification.Verify(n => n.SendNotificationToUserAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData("Confirmed", "NotifOrderConfirmedTitle", "NotifOrderConfirmedBody", "OrderConfirmed")]
    [InlineData("Preparing", "NotifOrderPreparingTitle", "NotifOrderPreparingBody", "OrderPreparing")]
    [InlineData("ReadyForPickup", "NotifOrderReadyForPickupTitle", "NotifOrderReadyForPickupBody", "OrderReadyForPickup")]
    [InlineData("Completed", "NotifOrderCompletedTitle", "NotifOrderCompletedBody", "OrderCompleted")]
    [InlineData("Cancelled", "NotifOrderCancelledTitle", "NotifOrderCancelledBody", "OrderCancelled")]
    public async Task UpdateOrderStatus_should_dispatch_exactly_one_customer_notification_of_correct_type(string statusStr, string expectedTitleKey, string expectedBodyKey, string expectedNotificationType)
    {
        // Arrange
        var customer = new ApplicationUser { Id = _userId, UserName = "cust-status@example.com", Email = "cust-status@example.com", FullName = "Customer Name", Status = UserStatus.Active };
        var merchant = new ApplicationUser { Id = Guid.NewGuid(), UserName = "merch-status@example.com", Email = "merch-status@example.com", FullName = "Merchant Name", Status = UserStatus.Active };
        _db.Users.AddRange(customer, merchant);

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = merchant.Id,
            Name = "Status Bakery",
            VerificationStatus = VerificationStatus.Verified
        };
        _db.Organizations.Add(org);

        var category = new Category { Id = Guid.NewGuid(), Name = "Bakery" };
        _db.Categories.Add(category);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            CategoryId = category.Id,
            Title = "Cake Slice",
            OriginalPrice = 20.0m,
            DiscountedPrice = 18.0m,
            QuantityAvailable = 5,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            Status = ProductStatus.Active
        };
        _db.Products.Add(product);

        var order = new Order
        {
            UserId = customer.Id,
            OrderStatus = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Pending,
            TotalAmount = 18.0m
        };
        order.Items.Add(new OrderItem
        {
            ProductId = product.Id,
            Quantity = 1,
            UnitPrice = 18.0m,
            Product = product
        });
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var mockNotification = new Mock<IRealTimeNotificationService>();
        var mockAudit = new Mock<IAuditLogService>();
        var handler = new UpdateOrderStatusCommandHandler(_db, mockAudit.Object, mockNotification.Object);

        var command = new UpdateOrderStatusCommand(merchant.Id, order.Id, statusStr);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        // Assert consumer notification is dispatched once
        mockNotification.Verify(n => n.SendNotificationToUserAsync(
            customer.Id,
            expectedTitleKey,
            expectedBodyKey,
            expectedNotificationType,
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert total dispatches is exactly 1
        mockNotification.Verify(n => n.SendNotificationToUserAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
