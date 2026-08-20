using FluentAssertions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Infrastructure.Features.Admin.Queries;
using FoodLoop.Infrastructure.Features.Organizations.Queries;
using FoodLoop.Infrastructure.Features.Products.Commands;
using FoodLoop.Infrastructure.Features.Users.Queries;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Application.Features.Organizations.Queries;
using FoodLoop.Application.Features.Users.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Features.Organizations;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Disputes;

public class DisputeAndPolicyTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext = ApplicationDbContextFactory.Create();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly Guid _reporterId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();

    public DisputeAndPolicyTests()
    {
        // Seed default system settings
        var settings = new SystemSettings
        {
            Id = SystemSettings.SingletonId,
            MaxExpiredReportsBeforeDeactivation = 3
        };
        _dbContext.SystemSettings.Add(settings);

        // Seed reporter user
        var reporter = new ApplicationUser
        {
            Id = _reporterId,
            UserName = "reporter@example.com",
            FullName = "Reporter User",
            Status = UserStatus.Active
        };
        _dbContext.Users.Add(reporter);

        // Seed store owner user
        var owner = new ApplicationUser
        {
            Id = _ownerId,
            UserName = "owner@example.com",
            FullName = "Store Owner",
            Status = UserStatus.Active
        };
        _dbContext.Users.Add(owner);

        // Seed organization (store)
        var organization = new Organization
        {
            Id = _organizationId,
            OwnerId = _ownerId,
            Name = "Test Store",
            VerificationStatus = VerificationStatus.Verified,
            AdminNote = "Initial admin notes."
        };
        _dbContext.Organizations.Add(organization);

        // Seed product (default not expired, expiration tomorrow)
        var product = new Product
        {
            Id = _productId,
            OrganizationId = _organizationId,
            Title = "Test Product",
            ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Status = ProductStatus.Active
        };
        _dbContext.Products.Add(product);

        _dbContext.SaveChanges();
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task DisputeImage_RoundTrip_Succeeds()
    {
        // Arrange
        var mockStorage = new Mock<IFileStorageService>();
        var imageUrl = "https://example.com/dispute-proof.png";
        mockStorage.Setup(s => s.SaveAsync(It.IsAny<FoodLoop.Application.Common.Models.FileUploadRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(imageUrl);

        var handler = new ReportProductCommandHandler(_dbContext, _auditLogService.Object, null!, mockStorage.Object);
        var fileUpload = new FoodLoop.Application.Common.Models.FileUploadRequest
        {
            FileName = "dispute-proof.png",
            ContentType = "image/png",
            Content = new System.IO.MemoryStream(new byte[] { 1, 2, 3 })
        };
        var command = new ReportProductCommand(_reporterId, _productId, ProductReportReason.MisleadingInfo, "Product price is incorrect.", fileUpload);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var report = await _dbContext.ProductReports.FirstOrDefaultAsync(r => r.ProductId == _productId);
        report.Should().NotBeNull();
        report!.ImageUrl.Should().Be(imageUrl);

        // Query disputes via Admin query to ensure mapping is clean
        var queryHandler = new GetDisputesQueryHandler(_dbContext, null!);
        var disputesList = await queryHandler.Handle(new GetDisputesQuery(1, 10, null), CancellationToken.None);
        var disputeDto = disputesList.FirstOrDefault(d => d.ProductId == _productId);
        disputeDto.Should().NotBeNull();
        disputeDto!.ImageUrl.Should().Be(imageUrl);

        // Query dispute by ID
        var getByIdHandler = new GetDisputeByIdQueryHandler(_dbContext);
        var disputeById = await getByIdHandler.Handle(new GetDisputeByIdQuery(report.Id), CancellationToken.None);
        disputeById.ImageUrl.Should().Be(imageUrl);

        // Query disputes by Store
        var mockOrgRepo = new Mock<IOrganizationRepository>();
        mockOrgRepo.Setup(r => r.GetByOwnerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization
            {
                Id = _organizationId,
                OwnerId = _ownerId
            });

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Organizations).Returns(mockOrgRepo.Object);

        var getStoreHandler = new GetStoreDisputesQueryHandler(_dbContext, mockUow.Object);
        var storeDisputes = await getStoreHandler.Handle(new GetStoreDisputesQuery(_ownerId, 1, 10, null), CancellationToken.None);
        var storeDispute = storeDisputes.FirstOrDefault(d => d.ProductId == _productId);
        storeDispute.Should().NotBeNull();
        storeDispute!.ImageUrl.Should().Be(imageUrl);

        // Query disputes by My Reports
        var getMyReportsHandler = new GetMyReportsQueryHandler(_dbContext);
        var myReports = await getMyReportsHandler.Handle(new GetMyReportsQuery(_reporterId, 1, 10, null), CancellationToken.None);
        var myReport = myReports.FirstOrDefault(d => d.ProductId == _productId);
        myReport.Should().NotBeNull();
        myReport!.ImageUrl.Should().Be(imageUrl);
    }

    [Fact]
    public async Task Report_WithoutImage_ThrowsArgumentException()
    {
        // Arrange
        var handler = new ReportProductCommandHandler(_dbContext, _auditLogService.Object);
        var command = new ReportProductCommand(_reporterId, _productId, ProductReportReason.Spam, "Spam listing.", null!);

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Evidence image is required.");
    }

    [Fact]
    public async Task Report_WithDirectImageFile_UploadsViaFileStorageServiceAndSavesUrl()
    {
        // Arrange
        var mockStorage = new Mock<IFileStorageService>();
        var uploadedUrl = "https://cloudinary.com/reports/evidence123.jpg";
        mockStorage.Setup(s => s.SaveAsync(It.IsAny<FoodLoop.Application.Common.Models.FileUploadRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedUrl);

        var handler = new ReportProductCommandHandler(_dbContext, _auditLogService.Object, null!, mockStorage.Object);
        var fileUpload = new FoodLoop.Application.Common.Models.FileUploadRequest
        {
            FileName = "evidence.jpg",
            ContentType = "image/jpeg",
            Content = new System.IO.MemoryStream(new byte[] { 1, 2, 3 })
        };
        var command = new ReportProductCommand(_reporterId, _productId, ProductReportReason.WrongExpiry, "Wrong expiry date on label.", fileUpload);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var report = await _dbContext.ProductReports.FirstOrDefaultAsync(r => r.ProductId == _productId && r.Reason == "WrongExpiry");
        report.Should().NotBeNull();
        report!.ImageUrl.Should().Be(uploadedUrl);
        mockStorage.Verify(s => s.SaveAsync(It.IsAny<FoodLoop.Application.Common.Models.FileUploadRequest>(), $"reports/{_productId}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ThresholdTrigger_DeactivatesStore_SuspendsMerchant_AppendsToNote()
    {
        // Arrange
        var handler = new ReportProductCommandHandler(_dbContext, _auditLogService.Object);

        // Seed 2 expired products and report them by distinct users to get count to 2
        var expProd1 = new Product { Id = Guid.NewGuid(), OrganizationId = _organizationId, Title = "Exp1", ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)) };
        var expProd2 = new Product { Id = Guid.NewGuid(), OrganizationId = _organizationId, Title = "Exp2", ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)) };
        _dbContext.Products.AddRange(expProd1, expProd2);

        var rep1 = new ProductReport { ProductId = expProd1.Id, ReportedBy = Guid.NewGuid(), Reason = "Expired", ImageUrl = "https://example.com/proof1.png", CreatedAt = DateTimeOffset.UtcNow };
        var rep2 = new ProductReport { ProductId = expProd2.Id, ReportedBy = Guid.NewGuid(), Reason = "WrongExpiry", ImageUrl = "https://example.com/proof2.png", CreatedAt = DateTimeOffset.UtcNow };
        _dbContext.ProductReports.AddRange(rep1, rep2);
        await _dbContext.SaveChangesAsync();

        // 3rd expired report by distinct user will trigger deactivation
        var expProd3 = new Product { Id = Guid.NewGuid(), OrganizationId = _organizationId, Title = "Exp3", ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) };
        _dbContext.Products.Add(expProd3);
        await _dbContext.SaveChangesAsync();

        var fileUpload = new FoodLoop.Application.Common.Models.FileUploadRequest
        {
            FileName = "proof3.png",
            ContentType = "image/png",
            Content = new System.IO.MemoryStream(new byte[] { 1, 2, 3 })
        };
        var command = new ReportProductCommand(_reporterId, expProd3.Id, ProductReportReason.Expired, "Third expired product reported.", fileUpload);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var org = await _dbContext.Organizations.FindAsync(_organizationId);
        org!.VerificationStatus.Should().Be(VerificationStatus.Rejected);
        org.AdminNote.Should().Contain("Initial admin notes.");
        org.AdminNote.Should().Contain("Auto-deactivated: Exceeded maximum allowed unresolved expired product reports (3/3 distinct customer strikes).");

        var merchant = await _dbContext.Users.FindAsync(_ownerId);
        merchant!.Status.Should().Be(UserStatus.Suspended);
    }

    [Fact]
    public async Task NonExpiredReport_DoesNotTriggerDeactivation()
    {
        // Arrange
        var handler = new ReportProductCommandHandler(_dbContext, _auditLogService.Object);
        var fileUpload = new FoodLoop.Application.Common.Models.FileUploadRequest
        {
            FileName = "spam.png",
            ContentType = "image/png",
            Content = new System.IO.MemoryStream(new byte[] { 1, 2, 3 })
        };

        // Add 5 non-expired reports
        for (int i = 0; i < 5; i++)
        {
            var command = new ReportProductCommand(_reporterId, _productId, ProductReportReason.Spam, $"Non-expired report {i}.", fileUpload);
            await handler.Handle(command, CancellationToken.None);
        }

        // Assert store remains Verified
        var org = await _dbContext.Organizations.FindAsync(_organizationId);
        org!.VerificationStatus.Should().Be(VerificationStatus.Verified);
        org.AdminNote.Should().Be("Initial admin notes.");

        var merchant = await _dbContext.Users.FindAsync(_ownerId);
        merchant!.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public async Task Report_NonExistentProduct_ThrowsNotFoundException()
    {
        // Arrange
        var handler = new ReportProductCommandHandler(_dbContext, _auditLogService.Object);
        var nonExistentId = Guid.NewGuid();
        var fileUpload = new FoodLoop.Application.Common.Models.FileUploadRequest
        {
            FileName = "proof.png",
            ContentType = "image/png",
            Content = new System.IO.MemoryStream(new byte[] { 1, 2, 3 })
        };
        var command = new ReportProductCommand(_reporterId, nonExistentId, ProductReportReason.Expired, "Details", fileUpload);

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<FoodLoop.Application.Common.Exceptions.NotFoundException>();
    }

    [Fact]
    public async Task Report_SoftDeletedProduct_ThrowsNotFoundException()
    {
        // Arrange
        var deletedProduct = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            Title = "Deleted Item",
            IsDeleted = true,
            ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))
        };
        _dbContext.Products.Add(deletedProduct);
        await _dbContext.SaveChangesAsync();

        var handler = new ReportProductCommandHandler(_dbContext, _auditLogService.Object);
        var fileUpload = new FoodLoop.Application.Common.Models.FileUploadRequest
        {
            FileName = "proof.png",
            ContentType = "image/png",
            Content = new System.IO.MemoryStream(new byte[] { 1, 2, 3 })
        };
        var command = new ReportProductCommand(_reporterId, deletedProduct.Id, ProductReportReason.Expired, "Details", fileUpload);

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<FoodLoop.Application.Common.Exceptions.NotFoundException>();
    }

    [Fact]
    public async Task Report_DispatchesNotificationAndAuditLog()
    {
        // Arrange
        var mockNotification = new Mock<IRealTimeNotificationService>();
        var mockAudit = new Mock<IAuditLogService>();
        var handler = new ReportProductCommandHandler(_dbContext, mockAudit.Object, mockNotification.Object);
        var fileUpload = new FoodLoop.Application.Common.Models.FileUploadRequest
        {
            FileName = "proof.jpg",
            ContentType = "image/jpeg",
            Content = new System.IO.MemoryStream(new byte[] { 1, 2, 3 })
        };
        var command = new ReportProductCommand(_reporterId, _productId, ProductReportReason.MisleadingInfo, "Price changed on shelf.", fileUpload);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        mockAudit.Verify(a => a.LogAsync(
            _reporterId,
            _organizationId,
            "ProductReported",
            "Product Reported by Customer",
            It.Is<string>(msg => msg.Contains("Test Product") && msg.Contains("MisleadingInfo")),
            null,
            It.IsAny<CancellationToken>()), Times.Once);

        mockNotification.Verify(n => n.SendNotificationToRoleAsync(
            "Admin",
            "NotifProductReportedTitle",
            "NotifProductReportedBody",
            "ProductReported",
            It.Is<object[]>(args => args.Length == 2 && (string)args[0] == "Test Product" && (string)args[1] == "MisleadingInfo"),
            "ProductReport",
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
