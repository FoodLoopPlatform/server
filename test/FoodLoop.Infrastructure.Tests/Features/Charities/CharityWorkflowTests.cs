using FluentAssertions;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.Features.Organizations.Commands;
using FoodLoop.Application.Features.Organizations.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Organizations.Commands;
using FoodLoop.Infrastructure.Features.Organizations.Queries;
using FoodLoop.Infrastructure.Features.Products;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Charities;

public class CharityWorkflowTests : IDisposable
{
    private readonly ApplicationDbContext _db = ApplicationDbContextFactory.Create();
    private readonly UnitOfWork _unitOfWork;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager = MockUserManagerFactory.Create();
    private readonly Mock<IAuditLogService> _mockAudit = new();
    private readonly Mock<IFileStorageService> _mockFileStorage = new();
    private readonly Mock<ILocalizationService> _mockLoc = MockLocalizationServiceFactory.Create();

    private readonly Guid _charityUserId = Guid.NewGuid();
    private readonly Guid _charityOrgId = Guid.NewGuid();
    private readonly Guid _donorOwnerId = Guid.NewGuid();
    private readonly Guid _donorOrgId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();

    public CharityWorkflowTests()
    {
        _unitOfWork = new UnitOfWork(_db);

        var charityUser = new ApplicationUser
        {
            Id = _charityUserId,
            UserName = "charity@hope.org",
            NormalizedUserName = "CHARITY@HOPE.ORG",
            Email = "charity@hope.org",
            NormalizedEmail = "CHARITY@HOPE.ORG",
            FullName = "Hope Charity Coordinator",
            Status = UserStatus.Active
        };

        var donorUser = new ApplicationUser
        {
            Id = _donorOwnerId,
            UserName = "donor@bakery.com",
            NormalizedUserName = "DONOR@BAKERY.COM",
            Email = "donor@bakery.com",
            NormalizedEmail = "DONOR@BAKERY.COM",
            FullName = "Bakery Owner",
            Status = UserStatus.Active
        };

        _db.Users.AddRange(charityUser, donorUser);

        var charityOrg = new Organization
        {
            Id = _charityOrgId,
            OwnerId = _charityUserId,
            Name = "Hope Food Bank",
            Email = "charity@hope.org",
            VerificationStatus = VerificationStatus.Verified
        };

        var donorOrg = new Organization
        {
            Id = _donorOrgId,
            OwnerId = _donorOwnerId,
            Name = "Downtown Bakery",
            Email = "donor@bakery.com",
            VerificationStatus = VerificationStatus.Verified
        };

        _db.Organizations.AddRange(charityOrg, donorOrg);

        var category = new Category { Id = Guid.NewGuid(), Name = "Bakery Goods" };
        _db.Categories.Add(category);

        var product = new Product
        {
            Id = _productId,
            OrganizationId = _donorOrgId,
            CategoryId = category.Id,
            Title = "Fresh Bread Loaves",
            OriginalPrice = 15m,
            DiscountedPrice = 8m,
            QuantityAvailable = 20,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            Status = ProductStatus.Active
        };
        _db.Products.Add(product);

        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact(DisplayName = "TC-CHA-01: GetCharitiesQuery only returns verified charities in Charity role")]
    public async Task GetCharitiesQuery_FiltersNonVerifiedAndNonCharity()
    {
        var charityUser = await _db.Users.FindAsync(_charityUserId);
        _mockUserManager.Setup(m => m.GetUsersInRoleAsync(AppRole.Charity))
            .ReturnsAsync(new List<ApplicationUser> { charityUser! });

        // Add an unverified charity
        var unverifiedUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = "unverified@charity.org", Email = "unverified@charity.org" };
        _db.Users.Add(unverifiedUser);
        var unverifiedOrg = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = unverifiedUser.Id,
            Name = "Unverified Charity",
            VerificationStatus = VerificationStatus.Pending
        };
        _db.Organizations.Add(unverifiedOrg);
        await _db.SaveChangesAsync();

        var handler = new GetCharitiesQueryHandler(_db, _mockUserManager.Object);
        var result = await handler.Handle(new GetCharitiesQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Hope Food Bank");
    }

    [Fact(DisplayName = "TC-CHA-02: Donating surplus to unverified charity throws NotFoundException")]
    public async Task DonateSurplus_UnverifiedRecipient_ThrowsNotFoundException()
    {
        var unverifiedId = Guid.NewGuid();
        _db.Organizations.Add(new Organization
        {
            Id = unverifiedId,
            OwnerId = Guid.NewGuid(),
            Name = "Pending Org",
            VerificationStatus = VerificationStatus.Pending
        });
        await _db.SaveChangesAsync();

        var handler = new DonateSurplusCommandHandler(_unitOfWork, _db, _mockAudit.Object);
        var command = new DonateSurplusCommand(
            DonorOwnerId: _donorOwnerId,
            RecipientOrganizationId: unverifiedId,
            ProductId: _productId,
            Quantity: 5,
            Note: "End of day surplus"
        );

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact(DisplayName = "TC-CHA-03: Donating zero or negative quantity throws ArgumentException")]
    public async Task DonateSurplus_ZeroOrNegativeQuantity_ThrowsArgumentException()
    {
        var handler = new DonateSurplusCommandHandler(_unitOfWork, _db, _mockAudit.Object);
        var command = new DonateSurplusCommand(
            DonorOwnerId: _donorOwnerId,
            RecipientOrganizationId: _charityOrgId,
            ProductId: _productId,
            Quantity: 0,
            Note: "Invalid quantity"
        );

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Quantity must be greater than zero.");
    }

    [Fact(DisplayName = "TC-CHA-04: Donating quantity exceeding stock throws ArgumentException")]
    public async Task DonateSurplus_ExceedingStock_ThrowsArgumentException()
    {
        var handler = new DonateSurplusCommandHandler(_unitOfWork, _db, _mockAudit.Object);
        var command = new DonateSurplusCommand(
            DonorOwnerId: _donorOwnerId,
            RecipientOrganizationId: _charityOrgId,
            ProductId: _productId,
            Quantity: 999, // only 20 available
            Note: "Too many"
        );

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Cannot donate 999 units; only 20 available.*");
    }

    [Fact(DisplayName = "TC-CHA-05: Valid surplus donation creates donation and deducts donor stock")]
    public async Task DonateSurplus_ValidDonation_CreatesDonationAndDeductsStock()
    {
        var handler = new DonateSurplusCommandHandler(_unitOfWork, _db, _mockAudit.Object);
        var command = new DonateSurplusCommand(
            DonorOwnerId: _donorOwnerId,
            RecipientOrganizationId: _charityOrgId,
            ProductId: _productId,
            Quantity: 8, // 20 - 8 = 12
            Note: "Fresh daily bread"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Quantity.Should().Be(8);
        result.Status.Should().Be("Pending");
        result.DonorName.Should().Be("Downtown Bakery");
        result.RecipientName.Should().Be("Hope Food Bank");

        var product = await _db.Products.FindAsync(_productId);
        product!.QuantityAvailable.Should().Be(12);

        var donation = await _db.Donations.FirstOrDefaultAsync(d => d.Id == result.Id);
        donation.Should().NotBeNull();
        donation!.Quantity.Should().Be(8);
    }

    [Fact(DisplayName = "TC-CHA-06: Charity uploading merchant-only document type throws ArgumentException")]
    public async Task UploadDocument_CharityUploadingMerchantType_ThrowsArgumentException()
    {
        var charityUser = await _db.Users.FindAsync(_charityUserId);
        _mockUserManager.Setup(m => m.FindByIdAsync(_charityUserId.ToString()))
            .ReturnsAsync(charityUser);
        _mockUserManager.Setup(m => m.IsInRoleAsync(charityUser!, AppRole.Charity))
            .ReturnsAsync(true);

        var handler = new UploadStoreDocumentCommandHandler(
            _unitOfWork,
            _mockFileStorage.Object,
            _mockLoc.Object,
            _mockUserManager.Object,
            _mockAudit.Object);

        // CommercialRegistration is only for Merchants, not Charities
        var command = new UploadOrganizationDocumentCommand(
            "charity@hope.org",
            UploadDocumentType.CommercialRegistration,
            new FileUploadRequest
            {
                Content = new MemoryStream("fake doc"u8.ToArray()),
                FileName = "reg.pdf",
                ContentType = "application/pdf"
            });

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*InvalidCharityDocumentType*");
    }

    [Fact(DisplayName = "TC-CHA-07: Re-uploading document replaces prior upload rather than duplicating")]
    public async Task UploadDocument_ReuploadSameType_ReplacesPriorRecord()
    {
        var charityUser = await _db.Users.FindAsync(_charityUserId);
        _mockUserManager.Setup(m => m.FindByIdAsync(_charityUserId.ToString()))
            .ReturnsAsync(charityUser);
        _mockUserManager.Setup(m => m.IsInRoleAsync(charityUser!, AppRole.Charity))
            .ReturnsAsync(true);

        _mockFileStorage.Setup(f => f.SaveAsync(It.IsAny<FileUploadRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://cdn.foodloop.com/docs/cert.pdf");

        var handler = new UploadStoreDocumentCommandHandler(
            _unitOfWork,
            _mockFileStorage.Object,
            _mockLoc.Object,
            _mockUserManager.Object,
            _mockAudit.Object);

        var command = new UploadOrganizationDocumentCommand(
            "charity@hope.org",
            UploadDocumentType.AssociationCertificate,
            new FileUploadRequest
            {
                Content = new MemoryStream("fake cert"u8.ToArray()),
                FileName = "cert.pdf",
                ContentType = "application/pdf"
            });

        // First upload
        await handler.Handle(command, CancellationToken.None);

        // Second upload with updated file
        _mockFileStorage.Setup(f => f.SaveAsync(It.IsAny<FileUploadRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://cdn.foodloop.com/docs/cert_v2.pdf");

        await handler.Handle(command, CancellationToken.None);

        var verifications = await _db.OrganizationVerifications
            .Where(v => v.OrganizationId == _charityOrgId && v.VerificationType == UploadDocumentType.AssociationCertificate)
            .ToListAsync();

        verifications.Should().HaveCount(1);
        verifications[0].DocumentUrl.Should().Be("https://cdn.foodloop.com/docs/cert_v2.pdf");
    }
}
