using FluentAssertions;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Orders.Queries;
using FoodLoop.Application.Features.Organizations.Commands;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Orders.Queries;
using FoodLoop.Infrastructure.Features.Organizations.Commands;
using FoodLoop.Infrastructure.Features.Products;
using FoodLoop.Infrastructure.Features.Products.Commands;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Security;

public class TenantIsolationAndSecurityTests : IDisposable
{
    private readonly ApplicationDbContext _db = ApplicationDbContextFactory.Create();
    private readonly UnitOfWork _unitOfWork;
    private readonly Mock<IAuditLogService> _mockAudit = new();
    private readonly Mock<IRealTimeNotificationService> _mockNotification = new();
    private readonly Mock<IFileStorageService> _mockFileStorage = new();

    private readonly Guid _merchantAId = Guid.NewGuid();
    private readonly Guid _orgAId = Guid.NewGuid();
    private readonly Guid _productAId = Guid.NewGuid();

    private readonly Guid _merchantBId = Guid.NewGuid();
    private readonly Guid _orgBId = Guid.NewGuid();
    private readonly Guid _productBId = Guid.NewGuid();

    private readonly Guid _categoryId = Guid.NewGuid();

    public TenantIsolationAndSecurityTests()
    {
        _unitOfWork = new UnitOfWork(_db);

        // Merchant A and Org A
        var merchantA = new ApplicationUser
        {
            Id = _merchantAId,
            UserName = "merchantA@test.com",
            Email = "merchantA@test.com",
            FullName = "Merchant A",
            Status = UserStatus.Active
        };

        var orgA = new Organization
        {
            Id = _orgAId,
            OwnerId = _merchantAId,
            Name = "Org A",
            VerificationStatus = VerificationStatus.Verified
        };

        // Merchant B and Org B
        var merchantB = new ApplicationUser
        {
            Id = _merchantBId,
            UserName = "merchantB@test.com",
            Email = "merchantB@test.com",
            FullName = "Merchant B",
            Status = UserStatus.Active
        };

        var orgB = new Organization
        {
            Id = _orgBId,
            OwnerId = _merchantBId,
            Name = "Org B",
            VerificationStatus = VerificationStatus.Verified
        };

        _db.Users.AddRange(merchantA, merchantB);
        _db.Organizations.AddRange(orgA, orgB);

        var category = new Category
        {
            Id = _categoryId,
            Name = "Groceries"
        };
        _db.Categories.Add(category);

        var productA = new Product
        {
            Id = _productAId,
            OrganizationId = _orgAId,
            CategoryId = _categoryId,
            Title = "Merchant A Product",
            OriginalPrice = 100m,
            DiscountedPrice = 60m,
            QuantityAvailable = 10,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
            Status = ProductStatus.Active
        };

        var productB = new Product
        {
            Id = _productBId,
            OrganizationId = _orgBId,
            CategoryId = _categoryId,
            Title = "Merchant B Product",
            OriginalPrice = 80m,
            DiscountedPrice = 40m,
            QuantityAvailable = 5,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
            Status = ProductStatus.Active
        };

        _db.Products.AddRange(productA, productB);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact(DisplayName = "TC-ISO-01: Merchant B cannot update Merchant A's product")]
    public async Task UpdateProduct_CrossTenant_ThrowsNotFoundException()
    {
        var handler = new UpdateProductCommandHandler(_unitOfWork, _mockAudit.Object);
        var command = new UpdateProductCommand(
            OwnerId: _merchantBId,
            ProductId: _productAId, // Merchant A's product
            CategoryId: null,
            Title: "Hacked Title",
            Description: null,
            OriginalPrice: null,
            DiscountedPrice: null,
            QuantityAvailable: null,
            ExpirationDate: null,
            Status: null
        );

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        var product = await _db.Products.FindAsync(_productAId);
        product!.Title.Should().Be("Merchant A Product");
    }

    [Fact(DisplayName = "TC-ISO-02: Merchant B cannot delete Merchant A's product")]
    public async Task DeleteProduct_CrossTenant_ThrowsNotFoundException()
    {
        var handler = new DeleteProductCommandHandler(_unitOfWork, _mockAudit.Object);
        var command = new DeleteProductCommand(
            OwnerId: _merchantBId,
            ProductId: _productAId
        );

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        var product = await _db.Products.FindAsync(_productAId);
        product.Should().NotBeNull();
        product!.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "TC-ISO-03: Merchant B cannot upload image to Merchant A's product")]
    public async Task UploadProductImage_CrossTenant_ThrowsNotFoundException()
    {
        var handler = new UploadProductImageCommandHandler(_unitOfWork, _mockFileStorage.Object, _mockAudit.Object);
        var command = new UploadProductImageCommand(
            OwnerId: _merchantBId,
            ProductId: _productAId,
            File: new FileUploadRequest
            {
                Content = new MemoryStream("fake image"u8.ToArray()),
                FileName = "test.png",
                ContentType = "image/png"
            }
        );

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact(DisplayName = "TC-ISO-04: Merchant B cannot delete image from Merchant A's product")]
    public async Task DeleteProductImage_CrossTenant_ThrowsNotFoundException()
    {
        var imageId = Guid.NewGuid();
        _db.ChangeTracker.Clear();
        var img = new ProductImage { Id = imageId, ProductId = _productAId, ImageUrl = "https://example.com/img.png" };
        _db.Set<ProductImage>().Add(img);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var handler = new DeleteProductImageCommandHandler(_unitOfWork, _mockAudit.Object);
        var command = new DeleteProductImageCommand(
            OwnerId: _merchantBId,
            ProductId: _productAId,
            ImageId: imageId
        );

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact(DisplayName = "TC-ISO-05: Customer B cannot view Customer A's order details")]
    public async Task GetOrderDetail_CrossUser_ThrowsUnauthorizedAccessException()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = _merchantAId, // Customer A
            TotalAmount = 50m,
            OrderStatus = OrderStatus.Completed,
            PaymentStatus = PaymentStatus.Paid
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var handler = new GetOrderDetailQueryHandler(_db);
        var query = new GetOrderDetailQuery(order.Id, _merchantBId); // Customer B querying Customer A's order

        var act = async () => await handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact(DisplayName = "TC-ISO-06: Merchant B cannot donate surplus from Merchant A's product")]
    public async Task DonateSurplus_CrossTenant_ThrowsNotFoundException()
    {
        var charityId = Guid.NewGuid();
        var charityOrg = new Organization
        {
            Id = charityId,
            OwnerId = Guid.NewGuid(),
            Name = "Charity Partner",
            VerificationStatus = VerificationStatus.Verified
        };
        _db.Organizations.Add(charityOrg);
        await _db.SaveChangesAsync();

        var handler = new DonateSurplusCommandHandler(_unitOfWork, _db, _mockAudit.Object);
        var command = new DonateSurplusCommand(
            DonorOwnerId: _merchantBId,
            RecipientOrganizationId: charityId,
            ProductId: _productAId, // Merchant A's product
            Quantity: 2,
            Note: "Surplus"
        );

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact(DisplayName = "TC-ISO-07: Unverified merchant cannot create products")]
    public async Task CreateProduct_UnverifiedMerchant_ThrowsArgumentException()
    {
        var unverifiedOwnerId = Guid.NewGuid();
        var unverifiedOrg = new Organization
        {
            Id = Guid.NewGuid(),
            OwnerId = unverifiedOwnerId,
            Name = "Pending Verification Store",
            VerificationStatus = VerificationStatus.Pending
        };
        _db.Organizations.Add(unverifiedOrg);
        await _db.SaveChangesAsync();

        var handler = new CreateProductCommandHandler(_unitOfWork, _mockAudit.Object, _mockNotification.Object);
        var command = new CreateProductCommand(
            unverifiedOwnerId,
            _categoryId,
            "Forbidden Product",
            "Should not be created",
            50m,
            25m,
            10,
            DateOnly.FromDateTime(DateTime.Today.AddDays(3))
        );

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*must be verified by an admin*");
    }
}
