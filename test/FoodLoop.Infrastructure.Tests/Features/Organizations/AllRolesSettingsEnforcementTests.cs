using FluentAssertions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Auth;
using FoodLoop.Application.DTOs.Notifications;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.Auth.Commands;
using FoodLoop.Application.Features.Organizations.Commands;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Application.Features.Users.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Auth.Commands;
using FoodLoop.Infrastructure.Features.Organizations.Commands;
using FoodLoop.Infrastructure.Features.Products.Commands;
using FoodLoop.Infrastructure.Features.Users.Commands;
using FoodLoop.Infrastructure.Hubs;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Persistence.Repositories;
using FoodLoop.Infrastructure.Services;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Organizations;

public class AllRolesSettingsEnforcementTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager = MockUserManagerFactory.Create();
    private readonly Mock<ILocalizationService> _mockLoc = new();
    private readonly Mock<IAuditLogService> _mockAudit = new();
    private readonly Mock<IEmailService> _mockEmail = new();
    private readonly Mock<IRealTimeNotificationService> _mockNotif = new();
    private readonly Mock<IFirebasePushNotificationService> _mockFirebase = new();
    private readonly Mock<IHubContext<NotificationHub, INotificationHubClient>> _mockHubContext = new();
    private readonly Mock<IHubClients<INotificationHubClient>> _mockClients = new();
    private readonly Mock<INotificationHubClient> _mockClientProxy = new();

    public AllRolesSettingsEnforcementTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()]).Returns<string>(s => s);
        _mockLoc.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns<string, object[]>((s, args) => string.Format(s, args));
        _mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(_mockClientProxy.Object);
        _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
    }

    [Fact]
    public async Task BulkUpload_WhenDisabledInSystemSettings_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var uow = new UnitOfWork(db);
        var ownerId = Guid.NewGuid();

        db.SystemSettings.Add(new SystemSettings
        {
            Id = SystemSettings.SingletonId,
            BulkProductUploadEnabled = false // Disabled by Platform Admin
        });

        db.Organizations.Add(new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = "Merchant Store",
            VerificationStatus = VerificationStatus.Verified
        });
        await db.SaveChangesAsync();

        var handler = new BulkUploadProductsCommandHandler(
            uow,
            _mockAudit.Object,
            _mockNotif.Object,
            NullLogger<BulkUploadProductsCommandHandler>.Instance);

        var csvContent = "title,originalprice,discountedprice,quantityavailable,expirationdate,categoryname\nApple,10,5,20,2026-12-31,General";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var fileDto = new FileUploadRequest
        {
            Content = stream,
            FileName = "products.csv",
            ContentType = "text/csv"
        };

        var command = new BulkUploadProductsCommand(ownerId, fileDto);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Bulk product upload is currently disabled*");
    }

    [Fact]
    public async Task MerchantRegistration_WhenAutoVerifyEnabled_ShouldCreateVerifiedOrganizationWithConfiguredAutomationMode()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var uow = new UnitOfWork(db);
        var userId = Guid.NewGuid();

        db.SystemSettings.Add(new SystemSettings
        {
            Id = SystemSettings.SingletonId,
            AutoVerifyPartnerStores = true,
            NewBusinessDefaultAutomationMode = AutomationMode.Autonomous
        });
        await db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "merchant@test.com",
            UserName = "merchant@test.com"
        };

        _mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<ApplicationUser, string>((u, _) => { u.Id = userId; });

        _mockUserManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("token-123");

        var handler = new RegisterCommandHandler(
            _mockUserManager.Object,
            uow,
            _mockEmail.Object,
            _mockLoc.Object,
            _mockAudit.Object,
            _mockNotif.Object);

        var command = new RegisterCommand(
            new RegisterRequest
            {
                Email = "merchant@test.com",
                Password = "Password123!",
                Name = "Merchant Name",
                PhoneNumber = "+201000000000",
                Role = AppRole.Merchant,
                BusinessName = "Autonomous Bakery",
                BusinessCategory = BusinessCategory.Bakery
            },
            "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var org = await db.Organizations.FirstOrDefaultAsync();
        org.Should().NotBeNull();
        org!.VerificationStatus.Should().Be(VerificationStatus.Verified);
        org.AiOperatingMode.Should().Be(AiOperatingMode.Autonomous);
        org.AiAutoPricingEnabled.Should().BeTrue();
        org.AiAutoDiscountEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAiSettings_ShouldSynchronizeOperatingModeAndFlags()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var uow = new UnitOfWork(db);
        var ownerId = Guid.NewGuid();

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = "Settings Bakery",
            AiOperatingMode = AiOperatingMode.Manual,
            AiAutoDiscountEnabled = false,
            AiAutoPricingEnabled = false
        };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var handler = new UpdateAiSettingsCommandHandler(uow, _mockAudit.Object);

        // Act 1: Update to Autonomous
        var command1 = new UpdateAiSettingsCommand(
            OwnerId: ownerId,
            AiAutoDiscountEnabled: null,
            AiAutoDiscountPercent: 15,
            AiAutoDiscountDaysBeforeExpiry: 4,
            AiAutoPricingEnabled: null,
            AutomationMode: AutomationMode.Autonomous);

        var res1 = await handler.Handle(command1, CancellationToken.None);

        // Assert 1
        res1.AiAutoPricingEnabled.Should().BeTrue();
        res1.AiAutoDiscountEnabled.Should().BeTrue();
        org.AiOperatingMode.Should().Be(AiOperatingMode.Autonomous);
        org.AiAutoDiscountDaysBeforeExpiry.Should().Be(4);

        // Act 2: Update to Manual
        var command2 = new UpdateAiSettingsCommand(
            OwnerId: ownerId,
            AiAutoDiscountEnabled: null,
            AiAutoDiscountPercent: 10,
            AiAutoDiscountDaysBeforeExpiry: 2,
            AiAutoPricingEnabled: null,
            AutomationMode: AutomationMode.Manual);

        var res2 = await handler.Handle(command2, CancellationToken.None);

        // Assert 2
        res2.AiAutoPricingEnabled.Should().BeFalse();
        res2.AiAutoDiscountEnabled.Should().BeFalse();
        org.AiOperatingMode.Should().Be(AiOperatingMode.Manual);
    }

    [Fact]
    public async Task RealTimeNotificationService_WhenOrderUpdatesDisabled_ShouldSuppressSignalRAndPush()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var userId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "customer@test.com",
            OrderUpdatesEnabled = false, // User explicitly opted out of Order Updates
            MarketingNotificationsEnabled = true,
            Language = "en"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new RealTimeNotificationService(
            db,
            _mockHubContext.Object,
            _mockFirebase.Object,
            _mockUserManager.Object,
            _mockLoc.Object,
            NullLogger<RealTimeNotificationService>.Instance);

        // Act: Send Order Update Notification
        await service.SendNotificationToUserAsync(
            userId,
            "OrderPlacedTitle",
            "OrderPlacedBody",
            "OrderPlaced",
            Array.Empty<object>(),
            CancellationToken.None);

        // Assert: Notification row is saved to database inbox, but push & real-time popup are suppressed
        var notif = await db.Notifications.FirstOrDefaultAsync();
        notif.Should().NotBeNull();
        notif!.UserId.Should().Be(userId);

        _mockClientProxy.Verify(c => c.ReceiveNotification(It.IsAny<NotificationDto>()), Times.Never);
        _mockFirebase.Verify(f => f.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RealTimeNotificationService_WhenMarketingDisabled_ShouldSuppressSignalRAndPushForMarketing()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var userId = Guid.NewGuid();

        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "customer2@test.com",
            OrderUpdatesEnabled = true,
            MarketingNotificationsEnabled = false, // Opted out of marketing
            Language = "en"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new RealTimeNotificationService(
            db,
            _mockHubContext.Object,
            _mockFirebase.Object,
            _mockUserManager.Object,
            _mockLoc.Object,
            NullLogger<RealTimeNotificationService>.Instance);

        // Act: Send Marketing Notification
        await service.SendNotificationToUserAsync(
            userId,
            "PromoTitle",
            "PromoBody",
            "Promotion",
            Array.Empty<object>(),
            CancellationToken.None);

        // Assert
        _mockClientProxy.Verify(c => c.ReceiveNotification(It.IsAny<NotificationDto>()), Times.Never);
        _mockFirebase.Verify(f => f.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePreferencesCommandHandler_ShouldPersistPreferencesCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            OrderUpdatesEnabled = true,
            MarketingNotificationsEnabled = true,
            Language = "en"
        };

        _mockUserManager.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _mockUserManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var handler = new UpdatePreferencesCommandHandler(_mockUserManager.Object);

        var command = new UpdatePreferencesCommand(
            userId,
            new UpdatePreferencesRequest
            {
                OrderUpdatesEnabled = false,
                MarketingNotificationsEnabled = false,
                PreferredLanguage = "ar"
            });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        user.OrderUpdatesEnabled.Should().BeFalse();
        user.MarketingNotificationsEnabled.Should().BeFalse();
        user.Language.Should().Be("ar");
        _mockUserManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }
}
