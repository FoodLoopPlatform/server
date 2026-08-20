using FluentAssertions;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Persistence.Repositories;
using FoodLoop.Infrastructure.Services;
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

namespace FoodLoop.Infrastructure.Tests.Services;

public class UserServiceAndOrganizationServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager = MockUserManagerFactory.Create();
    private readonly Mock<IFileStorageService> _mockFileStorage = new();

    [Fact]
    public async Task UserService_GetCurrentUser_ShouldReturnUserDtoWithRoles()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var uow = new UnitOfWork(db);
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            FullName = "Amr Khaled",
            Email = "amr@test.com",
            PhoneNumber = "+201234567890",
            Status = UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _mockUserManager.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { AppRole.Customer });

        var service = new UserService(_mockUserManager.Object, uow);

        // Act
        var result = await service.GetCurrentUserAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.FullName.Should().Be("Amr Khaled");
        result.Email.Should().Be("amr@test.com");
        result.Roles.Should().Contain(AppRole.Customer);
    }

    [Fact]
    public async Task UserService_UpdateProfileAndPreferences_ShouldUpdateEntity()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var uow = new UnitOfWork(db);
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            FullName = "Old Name",
            Email = "test@test.com",
            Language = "en",
            OrderUpdatesEnabled = true,
            MarketingNotificationsEnabled = false
        };

        _mockUserManager.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { AppRole.Customer });
        _mockUserManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var service = new UserService(_mockUserManager.Object, uow);

        // Act - Update Profile
        var profileResult = await service.UpdateProfileAsync(userId, new UpdateProfileRequest
        {
            Name = "New Name",
            PreferredLanguage = "ar",
            ProfileImage = "https://example.com/pic.jpg"
        });

        // Assert Profile
        profileResult.FullName.Should().Be("New Name");
        profileResult.Language.Should().Be("ar");
        profileResult.ProfileImage.Should().Be("https://example.com/pic.jpg");

        // Act - Update Preferences
        var prefResult = await service.UpdatePreferencesAsync(userId, new UpdatePreferencesRequest
        {
            OrderUpdatesEnabled = false,
            MarketingNotificationsEnabled = true
        });

        // Assert Preferences
        prefResult.Success.Should().BeTrue();
        user.OrderUpdatesEnabled.Should().BeFalse();
        user.MarketingNotificationsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task UserService_AddressManagement_ShouldCreateUpdateAndClearDefaults()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var uow = new UnitOfWork(db);
        var userId = Guid.NewGuid();
        var service = new UserService(_mockUserManager.Object, uow);

        // Act - Create Address 1 (Default)
        var addr1 = await service.CreateAddressAsync(userId, new CreateAddressRequest
        {
            AddressType = AddressType.Home,
            City = "Cairo",
            District = "Maadi",
            Street = "Street 9",
            BuildingNo = "12",
            IsDefault = true
        });

        addr1.Should().NotBeNull();
        addr1.IsDefault.Should().BeTrue();

        // Act - Create Address 2 (New Default)
        var addr2 = await service.CreateAddressAsync(userId, new CreateAddressRequest
        {
            AddressType = AddressType.Company,
            City = "Giza",
            District = "Dokki",
            Street = "Tahrir St",
            BuildingNo = "5",
            IsDefault = true
        });

        // Assert - addr2 is default, addr1 is no longer default
        var addresses = await service.GetAddressesAsync(userId);
        addresses.Should().HaveCount(2);
        var dbAddr1 = addresses.First(a => a.Id == addr1.Id);
        var dbAddr2 = addresses.First(a => a.Id == addr2.Id);
        dbAddr1.IsDefault.Should().BeFalse();
        dbAddr2.IsDefault.Should().BeTrue();

        // Act - Update Address 1
        var updatedAddr1 = await service.UpdateAddressAsync(userId, addr1.Id, new UpdateAddressRequest
        {
            City = "New Cairo",
            Notes = "Near AUC"
        });

        updatedAddr1.City.Should().Be("New Cairo");
        updatedAddr1.Notes.Should().Be("Near AUC");

        // Act - Delete Address 2
        await service.DeleteAddressAsync(userId, addr2.Id);
        var remainingAddresses = await service.GetAddressesAsync(userId);
        remainingAddresses.Should().HaveCount(1);
    }

    [Fact]
    public async Task OrganizationService_GetAndLocationUpdate_ShouldSucceed()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var uow = new UnitOfWork(db);
        var ownerId = Guid.NewGuid();

        var store = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = "Al-Nour Market",
            BusinessCategory = BusinessCategory.Supermarket,
            Governorate = "Cairo",
            City = "Nasr City",
            VerificationStatus = VerificationStatus.Unverified
        };
        db.Organizations.Add(store);
        await db.SaveChangesAsync();

        var service = new OrganizationService(uow, _mockFileStorage.Object, _mockUserManager.Object);

        // Act - Get Organization
        var getResult = await service.GetMyOrganizationAsync(ownerId);
        getResult.Should().NotBeNull();
        getResult.Name.Should().Be("Al-Nour Market");

        // Act - Update Location
        var locResult = await service.UpdateLocationAsync(ownerId, new UpdateOrganizationLocationRequest
        {
            Governorate = "Giza",
            City = "Sheikh Zayed",
            Neighborhood = "District 1",
            Street = "Main St",
            Latitude = 30.0123,
            Longitude = 31.0456
        });

        // Assert
        locResult.Governorate.Should().Be("Giza");
        locResult.City.Should().Be("Sheikh Zayed");
        locResult.Latitude.Should().Be(30.0123);
    }

    [Fact]
    public async Task OrganizationService_UploadDocument_Merchant_ShouldUploadAndStore()
    {
        // Arrange
        using var db = ApplicationDbContextFactory.Create();
        var uow = new UnitOfWork(db);
        var ownerId = Guid.NewGuid();

        var ownerUser = new ApplicationUser { Id = ownerId, UserName = "merchant@test.com" };
        _mockUserManager.Setup(m => m.FindByIdAsync(ownerId.ToString())).ReturnsAsync(ownerUser);
        _mockUserManager.Setup(m => m.IsInRoleAsync(ownerUser, AppRole.Charity)).ReturnsAsync(false);

        var store = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = "Merchant Shop",
            VerificationStatus = VerificationStatus.Unverified
        };
        db.Organizations.Add(store);
        await db.SaveChangesAsync();

        _mockFileStorage.Setup(f => f.SaveAsync(It.IsAny<FileUploadRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://cloudinary.com/docs/tax_id.pdf");

        var service = new OrganizationService(uow, _mockFileStorage.Object, _mockUserManager.Object);

        using var memoryStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var file = new FileUploadRequest
        {
            Content = memoryStream,
            FileName = "tax_id.pdf",
            ContentType = "application/pdf"
        };

        // Act
        var result = await service.UploadDocumentAsync(ownerId, UploadDocumentType.TaxIdCertificate, file);

        // Assert
        result.Should().NotBeNull();
        result.Documents.Should().HaveCount(1);
        result.Documents.First().DocumentUrl.Should().Be("https://cloudinary.com/docs/tax_id.pdf");
        result.Documents.First().VerificationType.Should().Be(UploadDocumentType.TaxIdCertificate.ToString());
    }
}
