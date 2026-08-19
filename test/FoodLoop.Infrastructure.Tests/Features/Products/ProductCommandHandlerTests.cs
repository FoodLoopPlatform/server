using FluentAssertions;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Products;
using FoodLoop.Infrastructure.Features.Products.Commands;
using FoodLoop.Infrastructure.Features.Products.Queries;
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

namespace FoodLoop.Infrastructure.Tests.Listings;

public class ProductCommandHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext = ApplicationDbContextFactory.Create();
    private readonly UnitOfWork _unitOfWork;
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly Mock<IRealTimeNotificationService> _notificationService = new();

    public ProductCommandHandlerTests()
    {
        _unitOfWork = new UnitOfWork(_dbContext);

        // Seed initial organization & category
        var organization = new Organization
        {
            Id = _organizationId,
            OwnerId = _ownerId,
            Name = "Test Organization",
            VerificationStatus = VerificationStatus.Verified
        };
        _dbContext.Organizations.Add(organization);

        var category = new Category
        {
            Id = _categoryId,
            Name = "Test Category"
        };
        _dbContext.Categories.Add(category);

        _dbContext.SaveChanges();
    }

    public void Dispose() => _dbContext.Dispose();

    // ---------- CreateProductCommandHandler ----------

    [Fact]
    public async Task CreateProduct_should_create_product_successfully()
    {
        // Arrange
        var handler = new CreateProductCommandHandler(_unitOfWork, _auditLogService.Object, _notificationService.Object);
        var command = new CreateProductCommand(
            OwnerId: _ownerId,
            CategoryId: _categoryId,
            Title: "Fresh Apples",
            Description: "Crispy apples",
            OriginalPrice: 10.00m,
            DiscountedPrice: 5.00m,
            QuantityAvailable: 10,
            ExpirationDate: DateOnly.FromDateTime(DateTime.Today.AddDays(5))
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Fresh Apples");
        result.OriginalPrice.Should().Be(10.00m);
        result.DiscountedPrice.Should().Be(5.00m);

        var dbProduct = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == result.Id);
        dbProduct.Should().NotBeNull();
        dbProduct!.Title.Should().Be("Fresh Apples");
    }

    [Fact]
    public async Task CreateProduct_should_fail_when_discounted_price_greater_than_original()
    {
        // Arrange
        var handler = new CreateProductCommandHandler(_unitOfWork, _auditLogService.Object, _notificationService.Object);
        var command = new CreateProductCommand(
            OwnerId: _ownerId,
            CategoryId: _categoryId,
            Title: "Fresh Apples",
            Description: "Crispy apples",
            OriginalPrice: 10.00m,
            DiscountedPrice: 15.00m, // Invalid
            QuantityAvailable: 10,
            ExpirationDate: DateOnly.FromDateTime(DateTime.Today.AddDays(5))
        );

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
    }

    // ---------- UpdateProductCommandHandler ----------

    [Fact]
    public async Task UpdateProduct_should_update_product_successfully()
    {
        // Arrange
        var product = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CategoryId = _categoryId,
            Title = "Old Title",
            OriginalPrice = 20.00m,
            DiscountedPrice = 10.00m,
            QuantityAvailable = 5,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today),
            Status = ProductStatus.Active
        };
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateProductCommandHandler(_unitOfWork, _auditLogService.Object);
        var command = new UpdateProductCommand(
            OwnerId: _ownerId,
            ProductId: product.Id,
            CategoryId: null,
            Title: "New Title",
            Description: null,
            OriginalPrice: 30.00m,
            DiscountedPrice: 15.00m,
            QuantityAvailable: 20,
            ExpirationDate: null,
            Status: "SoldOut"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("New Title");
        result.OriginalPrice.Should().Be(30.00m);
        result.DiscountedPrice.Should().Be(15.00m);
        result.QuantityAvailable.Should().Be(20);
        result.Status.Should().Be("SoldOut");
    }

    // ---------- UploadProductImageCommandHandler & DeleteProductImageCommandHandler ----------

    [Fact]
    public async Task Upload_and_Delete_Image_should_manage_images_successfully()
    {
        // Arrange
        var product = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            CategoryId = _categoryId,
            Title = "Apple Pack",
            OriginalPrice = 10.00m,
            DiscountedPrice = 5.00m,
            QuantityAvailable = 5,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today)
        };
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        var mockFileStorage = new Mock<IFileStorageService>();
        mockFileStorage.Setup(fs => fs.SaveAsync(It.IsAny<FileUploadRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://example.com/uploads/product.png");

        var uploadHandler = new UploadProductImageCommandHandler(_unitOfWork, mockFileStorage.Object, _auditLogService.Object);
        var uploadCommand = new UploadProductImageCommand(
            OwnerId: _ownerId,
            ProductId: product.Id,
            File: new FileUploadRequest
            {
                Content = new MemoryStream("fake image"u8.ToArray()),
                FileName = "product.png",
                ContentType = "image/png"
            }
        );

        // Act - Upload
        var uploadResult = await uploadHandler.Handle(uploadCommand, CancellationToken.None);

        // Assert - Upload
        uploadResult.Should().NotBeNull();
        uploadResult.Images.Should().HaveCount(1);
        uploadResult.Images[0].ImageUrl.Should().Be("https://example.com/uploads/product.png");

        var imageId = uploadResult.Images[0].Id;

        // Act - Delete
        var deleteHandler = new DeleteProductImageCommandHandler(_unitOfWork, _auditLogService.Object);
        var deleteCommand = new DeleteProductImageCommand(
            OwnerId: _ownerId,
            ProductId: product.Id,
            ImageId: imageId
        );
        var deleteResult = await deleteHandler.Handle(deleteCommand, CancellationToken.None);

        // Assert - Delete
        deleteResult.Should().NotBeNull();
        deleteResult.Images.Should().BeEmpty();
    }

    // ---------- BulkUploadProductsCommandHandler ----------

    [Fact]
    public async Task BulkUpload_should_create_products_in_PendingModeration_status_and_retain_ExpiryVerificationState_and_notify_per_item()
    {
        // Arrange
        var csvContent = "title,originalprice,discountedprice,quantityavailable,expirationdate,categoryname,expiryverificationstate\n" +
                         "CSV Product 1,10.00,5.00,5,2026-12-31,Test Category,AiVerified\n" +
                         "CSV Product 2,20.00,10.00,10,2026-12-31,Test Category,AiLowConfidence";
        
        var uploadRequest = new FileUploadRequest
        {
            Content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csvContent)),
            FileName = "products.csv",
            ContentType = "text/csv"
        };

        var handler = new BulkUploadProductsCommandHandler(
            _unitOfWork,
            _auditLogService.Object,
            _notificationService.Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<BulkUploadProductsCommandHandler>>().Object
        );

        var command = new BulkUploadProductsCommand(_ownerId, uploadRequest);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);

        // Verify Database state
        var dbProducts = await _dbContext.Products
            .Where(p => p.Title.StartsWith("CSV Product"))
            .OrderBy(p => p.Title)
            .ToListAsync();

        dbProducts.Should().HaveCount(2);

        // 1st product:
        dbProducts[0].Title.Should().Be("CSV Product 1");
        dbProducts[0].Status.Should().Be(ProductStatus.PendingModeration); // Forced fail-closed
        dbProducts[0].ExpiryVerificationState.Should().Be(ExpiryVerificationState.AiVerified); // Retained

        // 2nd product:
        dbProducts[1].Title.Should().Be("CSV Product 2");
        dbProducts[1].Status.Should().Be(ProductStatus.PendingModeration); // Forced fail-closed
        dbProducts[1].ExpiryVerificationState.Should().Be(ExpiryVerificationState.AiLowConfidence); // Retained

        // Verify Notifications
        _notificationService.Verify(n => n.SendNotificationToRoleAsync(
            "Admin",
            "NotifProductModerationTitle",
            "NotifProductModerationBodyCsv",
            "ProductUploaded",
            It.Is<object[]>(args => args.Length == 2 && (string)args[0] == "CSV Product 1" && (string)args[1] == "Test Organization"),
            "Product",
            dbProducts[0].Id,
            It.IsAny<CancellationToken>()),
            Times.Once);

        _notificationService.Verify(n => n.SendNotificationToRoleAsync(
            "Admin",
            "NotifProductModerationTitle",
            "NotifProductModerationBodyCsv",
            "ProductUploaded",
            It.Is<object[]>(args => args.Length == 2 && (string)args[0] == "CSV Product 2" && (string)args[1] == "Test Organization"),
            "Product",
            dbProducts[1].Id,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BulkUpload_should_isolate_notification_failures_and_continue_processing()
    {
        // Arrange
        var csvContent = "title,originalprice,discountedprice,quantityavailable,expirationdate,categoryname,expiryverificationstate\n" +
                         "CSV Product Fail,10.00,5.00,5,2026-12-31,Test Category,AiVerified\n" +
                         "CSV Product Success,20.00,10.00,10,2026-12-31,Test Category,AiLowConfidence";
        
        var uploadRequest = new FileUploadRequest
        {
            Content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csvContent)),
            FileName = "products.csv",
            ContentType = "text/csv"
        };

        // Set up notification service to throw for CSV Product Fail, but succeed for CSV Product Success
        _notificationService.Setup(n => n.SendNotificationToRoleAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<object[]>(args => args.Length == 2 && (string)args[0] == "CSV Product Fail" && (string)args[1] == "Test Organization"),
            It.IsAny<string>(),
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("SignalR/FCM transient failure"));

        var handler = new BulkUploadProductsCommandHandler(
            _unitOfWork,
            _auditLogService.Object,
            _notificationService.Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<BulkUploadProductsCommandHandler>>().Object
        );

        var command = new BulkUploadProductsCommand(_ownerId, uploadRequest);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);

        // Verify both products were saved in DB
        var dbProducts = await _dbContext.Products
            .Where(p => p.Title.StartsWith("CSV Product"))
            .OrderBy(p => p.Title)
            .ToListAsync();

        dbProducts.Should().HaveCount(2);

        // Verify the notification for the second product was still attempted
        _notificationService.Verify(n => n.SendNotificationToRoleAsync(
            "Admin",
            "NotifProductModerationTitle",
            "NotifProductModerationBodyCsv",
            "ProductUploaded",
            It.Is<object[]>(args => args.Length == 2 && (string)args[0] == "CSV Product Success" && (string)args[1] == "Test Organization"),
            "Product",
            dbProducts[1].Id,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}





