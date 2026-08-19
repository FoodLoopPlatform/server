using FluentAssertions;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Admin.Commands;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Security;

public class RoleBasedAccessControlTests : IDisposable
{
    private readonly ApplicationDbContext _db = ApplicationDbContextFactory.Create();
    private readonly UnitOfWork _unitOfWork;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager = MockUserManagerFactory.Create();
    private readonly Mock<ICurrentUserService> _mockCurrentUser = new();
    private readonly Mock<IAuditLogService> _mockAudit = new();
    private readonly Mock<IEmailService> _mockEmail = new();

    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _merchantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();

    public RoleBasedAccessControlTests()
    {
        _unitOfWork = new UnitOfWork(_db);

        var adminUser = new ApplicationUser
        {
            Id = _adminId,
            UserName = "admin@foodloop.com",
            Email = "admin@foodloop.com",
            Status = UserStatus.Active
        };

        var merchantUser = new ApplicationUser
        {
            Id = _merchantId,
            UserName = "merchant@foodloop.com",
            NormalizedUserName = "MERCHANT@FOODLOOP.COM",
            Email = "merchant@foodloop.com",
            NormalizedEmail = "MERCHANT@FOODLOOP.COM",
            Status = UserStatus.PendingVerification
        };

        _db.Users.AddRange(adminUser, merchantUser);

        var org = new Organization
        {
            Id = _orgId,
            OwnerId = _merchantId,
            Name = "Pending Verification Bakery",
            VerificationStatus = VerificationStatus.Pending
        };
        _db.Organizations.Add(org);

        var cat = new Category { Id = Guid.NewGuid(), Name = "Bakery" };
        _db.Categories.Add(cat);

        var product = new Product
        {
            Id = _productId,
            OrganizationId = _orgId,
            CategoryId = cat.Id,
            Title = "Product for Moderation",
            OriginalPrice = 50m,
            DiscountedPrice = 25m,
            QuantityAvailable = 10,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            Status = ProductStatus.PendingModeration
        };
        _db.Products.Add(product);

        _db.SaveChanges();

        _mockCurrentUser.Setup(u => u.UserId).Returns(_adminId);
        _mockUserManager.Setup(m => m.FindByIdAsync(_merchantId.ToString())).ReturnsAsync(merchantUser);
    }

    public void Dispose() => _db.Dispose();

    [Fact(DisplayName = "TC-RBAC-01: Admin approves pending moderation product transitioning it to Active")]
    public async Task ModerateProduct_Approve_TransitionsToActive()
    {
        var handler = new ModerateProductCommandHandler(_db, _mockCurrentUser.Object, _mockAudit.Object);
        var command = new ModerateProductCommand(_productId, "Approve", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be("Active");
        result.ModerationNote.Should().BeNull();

        var prodInDb = await _db.Products.FindAsync(_productId);
        prodInDb!.Status.Should().Be(ProductStatus.Active);
    }

    [Fact(DisplayName = "TC-RBAC-02: Admin rejects pending product without reason note throws ArgumentException")]
    public async Task ModerateProduct_RejectWithoutNote_ThrowsArgumentException()
    {
        var handler = new ModerateProductCommandHandler(_db, _mockCurrentUser.Object, _mockAudit.Object);
        var command = new ModerateProductCommand(_productId, "Reject", "   "); // Empty note!

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*reason note is required*");
    }

    [Fact(DisplayName = "TC-RBAC-03: Admin verifies store activating organization and owner account")]
    public async Task VerifyStore_Approved_ActivatesStoreAndOwner()
    {
        var handler = new VerifyStoreCommandHandler(_unitOfWork, _mockUserManager.Object, _mockAudit.Object, _mockEmail.Object);
        var command = new VerifyOrganizationCommand(
            _orgId,
            _adminId,
            new VerifyOrganizationRequest { Action = "Approved", Note = "All documents valid." }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.VerificationStatus.Should().Be("Verified");

        var orgInDb = await _db.Organizations.FindAsync(_orgId);
        orgInDb!.VerificationStatus.Should().Be(VerificationStatus.Verified);
    }

    [Fact(DisplayName = "TC-RBAC-04: Admin softly deletes product marking IsDeleted true")]
    public async Task AdminDeleteProduct_MarksProductAsDeleted()
    {
        var handler = new AdminDeleteProductCommandHandler(_db);
        var command = new AdminDeleteProductCommand(_productId);

        await handler.Handle(command, CancellationToken.None);

        var prodInDb = await _db.Products.FindAsync(_productId);
        prodInDb!.IsDeleted.Should().BeTrue();
    }
}
