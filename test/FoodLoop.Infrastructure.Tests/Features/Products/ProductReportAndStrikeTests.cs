using FluentAssertions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Application.Features.Organizations.Commands;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Admin.Commands;
using FoodLoop.Infrastructure.Features.Organizations.Commands;
using FoodLoop.Infrastructure.Features.Products.Commands;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Products;

public class ProductReportAndStrikeTests
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<IAuditLogService> _auditLogMock;
    private readonly Mock<IRealTimeNotificationService> _notificationMock;
    private readonly Mock<IFileStorageService> _fileStorageMock;

    public ProductReportAndStrikeTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);
        _auditLogMock = new Mock<IAuditLogService>();
        _notificationMock = new Mock<IRealTimeNotificationService>();
        _fileStorageMock = new Mock<IFileStorageService>();

        _fileStorageMock
            .Setup(f => f.SaveAsync(It.IsAny<FileUploadRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://cloudinary.com/reports/evidence.jpg");
    }

    private async Task<(Organization org, ApplicationUser owner, Product product)> SeedStoreAsync()
    {
        var owner = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "merchant@store.com",
            FullName = "Store Owner",
            Status = UserStatus.Active
        };
        _db.Users.Add(owner);

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = owner.Id,
            Name = "Fresh Market",
            VerificationStatus = VerificationStatus.Verified
        };
        _db.Organizations.Add(org);

        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        _db.Categories.Add(category);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            CategoryId = category.Id,
            Title = "Fresh Milk 1L",
            OriginalPrice = 30m,
            DiscountedPrice = 15m,
            ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            Status = ProductStatus.Active
        };
        _db.Products.Add(product);

        var settings = new SystemSettings
        {
            Id = SystemSettings.SingletonId,
            MaxExpiredReportsBeforeDeactivation = 3
        };
        _db.SystemSettings.Add(settings);

        await _db.SaveChangesAsync();
        return (org, owner, product);
    }

    [Fact]
    public async Task AntiSabotage_SingleUser_SpammingMultipleReports_OnlyCountsAsOneStrike_DoesNotSuspend()
    {
        // Arrange
        var (org, owner, product) = await SeedStoreAsync();
        var competitorId = Guid.NewGuid();
        var file = new FileUploadRequest { Content = new MemoryStream(new byte[] { 1, 2, 3 }), FileName = "fake.jpg", ContentType = "image/jpeg" };

        var handler = new ReportProductCommandHandler(_db, _auditLogMock.Object, _notificationMock.Object, _fileStorageMock.Object);

        // Act - Same competitor submits 3 reports against the store
        for (int i = 0; i < 3; i++)
        {
            var command = new ReportProductCommand(
                ReportedBy: competitorId,
                ProductId: product.Id,
                Reason: ProductReportReason.Expired,
                Details: $"Competitor fake report #{i + 1}",
                ImageFile: file
            );
            await handler.Handle(command, CancellationToken.None);
        }

        // Assert - Store should NOT be suspended because all 3 reports came from 1 single customer (1 distinct strike)
        var updatedOrg = await _db.Organizations.FindAsync(org.Id);
        var updatedOwner = await _db.Users.FindAsync(owner.Id);

        updatedOrg!.VerificationStatus.Should().Be(VerificationStatus.Verified);
        updatedOwner!.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public async Task DistinctCustomers_ThreeStrikes_TriggersTieredWarnings_AndAutoSuspension()
    {
        // Arrange
        var (org, owner, product) = await SeedStoreAsync();
        var file = new FileUploadRequest { Content = new MemoryStream(new byte[] { 1, 2, 3 }), FileName = "evidence.jpg", ContentType = "image/jpeg" };
        var handler = new ReportProductCommandHandler(_db, _auditLogMock.Object, _notificationMock.Object, _fileStorageMock.Object);

        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();

        // Act 1: First strike (Distinct User 1)
        await handler.Handle(new ReportProductCommand(
            ReportedBy: user1,
            ProductId: product.Id,
            Reason: ProductReportReason.Expired,
            Details: "Expired milk on shelf",
            ImageFile: file
        ), CancellationToken.None);

        // Assert 1: Store remains verified, Warning 1/3 sent
        (await _db.Organizations.FindAsync(org.Id))!.VerificationStatus.Should().Be(VerificationStatus.Verified);
        _notificationMock.Verify(n => n.SendNotificationToUserAsync(
            owner.Id,
            "NotifDisputeWarningTitle",
            "NotifDisputeWarningBody",
            "DisputeWarning",
            It.IsAny<object[]>(),
            "ProductReport",
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Act 2: Second strike (Distinct User 2)
        await handler.Handle(new ReportProductCommand(
            ReportedBy: user2,
            ProductId: product.Id,
            Reason: ProductReportReason.WrongExpiry,
            Details: "Expiry label tampered",
            ImageFile: file
        ), CancellationToken.None);

        // Assert 2: Urgent Warning 2/3 sent
        (await _db.Organizations.FindAsync(org.Id))!.VerificationStatus.Should().Be(VerificationStatus.Verified);
        _notificationMock.Verify(n => n.SendNotificationToUserAsync(
            owner.Id,
            "NotifDisputeUrgentWarningTitle",
            "NotifDisputeUrgentWarningBody",
            "DisputeUrgentWarning",
            It.IsAny<object[]>(),
            "ProductReport",
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Act 3: Third strike (Distinct User 3)
        await handler.Handle(new ReportProductCommand(
            ReportedBy: user3,
            ProductId: product.Id,
            Reason: ProductReportReason.Expired,
            Details: "Spoiled yogurt",
            ImageFile: file
        ), CancellationToken.None);

        // Assert 3: Store Auto-Suspended!
        var suspendedOrg = await _db.Organizations.FindAsync(org.Id);
        var suspendedOwner = await _db.Users.FindAsync(owner.Id);

        suspendedOrg!.VerificationStatus.Should().Be(VerificationStatus.Rejected);
        suspendedOrg.AdminNote.Should().Contain("Auto-deactivated: Exceeded maximum allowed unresolved expired product reports");
        suspendedOwner!.Status.Should().Be(UserStatus.Suspended);

        // Store owner receives suspension notification
        _notificationMock.Verify(n => n.SendNotificationToUserAsync(
            owner.Id,
            "NotifStoreSuspendedTitle",
            "NotifStoreSuspendedBody",
            "StoreSuspended",
            It.IsAny<object[]>(),
            "Organization",
            org.Id,
            It.IsAny<CancellationToken>()), Times.Once);

        // Admin receives urgent auto-suspension alert
        _notificationMock.Verify(n => n.SendNotificationToRoleAsync(
            "Admin",
            "NotifStoreAutoSuspendedTitle",
            "NotifStoreAutoSuspendedBody",
            "StoreSuspended",
            It.IsAny<object[]>(),
            "Organization",
            org.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolvingDispute_DecrementsActiveStrikeCount_AndRefundsCustomer()
    {
        // Arrange
        var (org, owner, product) = await SeedStoreAsync();
        var customer = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "customer@buyer.com",
            FullName = "Happy Buyer",
            WalletBalance = 50m
        };
        _db.Users.Add(customer);

        var report = new ProductReport
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            ReportedBy = customer.Id,
            Reason = "Expired",
            Details = "Expired item",
            IsResolved = false,
            ImageUrl = "https://cloudinary.com/test.jpg"
        };
        _db.ProductReports.Add(report);
        await _db.SaveChangesAsync();

        var resolveHandler = new ResolveStoreDisputeCommandHandler(_db, _auditLogMock.Object, _notificationMock.Object);

        // Act - Merchant resolves dispute with 20 EGP wallet refund
        var result = await resolveHandler.Handle(new ResolveStoreDisputeCommand(
            DisputeId: report.Id,
            MerchantUserId: owner.Id,
            MerchantNote: "Apologies, batch was discarded. Full refund provided.",
            RefundAmount: 20m), CancellationToken.None);

        // Assert
        result.IsResolved.Should().BeTrue();
        var updatedCustomer = await _db.Users.FindAsync(customer.Id);
        updatedCustomer!.WalletBalance.Should().Be(70m); // 50 + 20

        var updatedReport = await _db.ProductReports.FindAsync(report.Id);
        updatedReport!.IsResolved.Should().BeTrue();
        updatedReport.AdminNote.Should().Contain("Refunded 20.00 to wallet");

        // Customer notified of refund
        _notificationMock.Verify(n => n.SendNotificationToUserAsync(
            customer.Id,
            "NotifDisputeResolvedCustomerTitle",
            "NotifDisputeResolvedCustomerBody",
            "DisputeRefunded",
            It.IsAny<object[]>(),
            "ProductReport",
            report.Id,
            It.IsAny<CancellationToken>()), Times.Once);

        // Merchant notified of decremented strikes (0 active strikes remaining)
        _notificationMock.Verify(n => n.SendNotificationToUserAsync(
            owner.Id,
            "NotifDisputeResolvedMerchantTitle",
            "NotifDisputeResolvedMerchantBody",
            "DisputeResolved",
            It.Is<object[]>(args => (int)args[1] == 0), // remainingActiveStrikes == 0
            "ProductReport",
            report.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
