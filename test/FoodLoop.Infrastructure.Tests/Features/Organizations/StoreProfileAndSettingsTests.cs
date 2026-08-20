using FluentAssertions;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Application.Features.Organizations.Commands;
using FoodLoop.Application.Features.Organizations.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Admin.Commands;
using FoodLoop.Infrastructure.Features.Admin.Queries;
using FoodLoop.Infrastructure.Features.Organizations.Commands;
using FoodLoop.Infrastructure.Features.Organizations.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Persistence.Repositories;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Organizations;

public class StoreProfileAndSettingsTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager = MockUserManagerFactory.Create();
    private readonly Mock<ILocalizationService> _mockLoc = new();
    private readonly Mock<IFileStorageService> _mockFileStorage = new();
    private readonly Mock<IAuditLogService> _mockAudit = new();

    public StoreProfileAndSettingsTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()]).Returns<string>(s => s);
    }

    [Fact]
    public async Task GetStoreProfile_ShouldReturnStoreWithReviewsAndRatingDistribution()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var uow = new UnitOfWork(db);
        var ownerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var store = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = "Sunny Bakery",
            Description = "Fresh baked artisanal breads",
            Logo = "https://example.com/logo.jpg",
            CoverPhoto = "https://example.com/cover.jpg",
            IsDeleted = false
        };
        db.Organizations.Add(store);

        var reviewer = new ApplicationUser
        {
            Id = customerId,
            UserName = "reviewer@test.com",
            FullName = "Reviewer User",
            ProfileImage = "https://example.com/user.jpg"
        };
        db.Users.Add(reviewer);

        var review1 = new Review
        {
            Id = Guid.NewGuid(),
            OrganizationId = store.Id,
            UserId = customerId,
            OrderId = Guid.NewGuid(),
            Rating = 5,
            Comment = "Delicious bread!"
        };
        var review2 = new Review
        {
            Id = Guid.NewGuid(),
            OrganizationId = store.Id,
            UserId = customerId,
            OrderId = Guid.NewGuid(),
            Rating = 4,
            Comment = "Good quality"
        };
        db.Reviews.AddRange(review1, review2);
        await db.SaveChangesAsync();

        var handler = new GetStoreProfileQueryHandler(uow, db, _mockUserManager.Object, _mockLoc.Object);

        // Act
        var result = await handler.Handle(new GetStoreProfileQuery(store.Id, ReviewsPageNumber: 1, ReviewsPageSize: 10), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(store.Id);
        result.Name.Should().Be("Sunny Bakery");
        result.TotalReviews.Should().Be(2);
        result.AverageRating.Should().Be(4.5);
        result.RatingDistribution.Should().HaveCount(5);
        result.RatingDistribution.First(d => d.Stars == 5).Count.Should().Be(1);
        result.RatingDistribution.First(d => d.Stars == 4).Count.Should().Be(1);
        result.RecentReviews.Should().HaveCount(2);
        result.RecentReviews.First().UserFullName.Should().Be("Reviewer User");
    }

    [Fact]
    public async Task UpdateStoreProfile_ShouldUpdateFieldsAndUploadFiles()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var uow = new UnitOfWork(db);
        var ownerId = Guid.NewGuid();

        var store = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = "Old Name",
            Description = "Old Desc",
            BusinessCategory = BusinessCategory.Cafe,
            IsDeleted = false
        };
        db.Organizations.Add(store);
        await db.SaveChangesAsync();

        _mockFileStorage.Setup(f => f.SaveAsync(It.IsAny<FileUploadRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://cloudinary.com/uploaded.png");

        var handler = new UpdateStoreProfileCommandHandler(uow, _mockFileStorage.Object, _mockLoc.Object, _mockAudit.Object);

        using var memoryStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileReq = new FileUploadRequest { Content = memoryStream, FileName = "logo.png", ContentType = "image/png" };

        var command = new UpdateOrganizationProfileCommand(ownerId, new UpdateOrganizationProfileRequest
        {
            Name = "New Super Name",
            Description = "New Description",
            BusinessCategory = BusinessCategory.Bakery,
            LogoFile = fileReq,
            Phone = "+201000000000",
            OpeningHours = "{\"mon\":\"8-20\"}"
        });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("New Super Name");
        result.BusinessCategory.Should().Be(BusinessCategory.Bakery);

        var updatedStore = await db.Organizations.FindAsync(store.Id);
        updatedStore!.Name.Should().Be("New Super Name");
        updatedStore.Description.Should().Be("New Description");
        updatedStore.Logo.Should().Be("https://cloudinary.com/uploaded.png");
        updatedStore.Phone.Should().Be("+201000000000");
    }

    [Fact]
    public async Task SaveSystemSettings_ShouldValidateAndPersistPlatformSettings()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var adminId = Guid.NewGuid();

        // Seed initial system settings singleton
        var initialSettings = new SystemSettings
        {
            Id = SystemSettings.SingletonId,
            MaxDiscountPerCyclePercent = 5,
            DefaultPriceFloorPolicy = PriceFloorPolicy.DynamicAi,
            NewBusinessDefaultAutomationMode = AutomationMode.Manual,
            PlatformCommissionPercent = 10,
            ApiRequestRateLimitPerMinute = 120
        };
        db.SystemSettings.Add(initialSettings);
        await db.SaveChangesAsync();

        var saveHandler = new SaveSystemSettingsCommandHandler(db, _mockAudit.Object);
        var getHandler = new GetSystemSettingsQueryHandler(db);

        var command = new SaveSystemSettingsCommand(
            AdminId: adminId,
            MaxDiscountPerCyclePercent: 12,
            DefaultPriceFloorPolicy: "Fixed30Percent",
            NewBusinessDefaultAutomationMode: "Autonomous",
            AutoVerifyPartnerStores: true,
            BulkProductUploadEnabled: true,
            PlatformCommissionPercent: 15,
            ApiRequestRateLimitPerMinute: 300
        );

        // Act
        var saveResult = await saveHandler.Handle(command, CancellationToken.None);

        // Assert Save Result
        saveResult.Should().NotBeNull();
        saveResult.MaxDiscountPerCyclePercent.Should().Be(12);
        saveResult.DefaultPriceFloorPolicy.Should().Be("Fixed30Percent");
        saveResult.NewBusinessDefaultAutomationMode.Should().Be("Autonomous");
        saveResult.PlatformCommissionPercent.Should().Be(15);
        saveResult.ApiRequestRateLimitPerMinute.Should().Be(300);

        // Act - Get via QueryHandler
        var getResult = await getHandler.Handle(new GetSystemSettingsQuery(), CancellationToken.None);
        getResult.Should().NotBeNull();
        getResult.MaxDiscountPerCyclePercent.Should().Be(12);
        getResult.AutoVerifyPartnerStores.Should().BeTrue();
    }
}
