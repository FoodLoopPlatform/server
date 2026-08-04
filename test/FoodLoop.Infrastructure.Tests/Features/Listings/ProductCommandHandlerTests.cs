using FluentAssertions;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Listings;
using FoodLoop.Application.Features.Listings.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Listings;
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
    private readonly Guid _storeId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();

    public ProductCommandHandlerTests()
    {
        _unitOfWork = new UnitOfWork(_dbContext);

        // Seed initial store & category
        var store = new Store
        {
            Id = _storeId,
            OwnerId = _ownerId,
            Name = "Test Store",
            VerificationStatus = VerificationStatus.Verified
        };
        _dbContext.Stores.Add(store);

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
        var handler = new CreateProductCommandHandler(_unitOfWork);
        var command = new CreateProductCommand(
            OwnerId: _ownerId,
            CategoryId: _categoryId,
            Title: "Fresh Apples",
            TitleAr: "تفاح طازج",
            Description: "Crispy apples",
            DescriptionAr: "تفاح مقرمش طازج",
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
        var handler = new CreateProductCommandHandler(_unitOfWork);
        var command = new CreateProductCommand(
            OwnerId: _ownerId,
            CategoryId: _categoryId,
            Title: "Fresh Apples",
            TitleAr: "تفاح طازج",
            Description: "Crispy apples",
            DescriptionAr: "تفاح مقرمش طازج",
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
            StoreId = _storeId,
            CategoryId = _categoryId,
            Title = "Old Title",
            OriginalPrice = 20.00m,
            DiscountedPrice = 10.00m,
            QuantityAvailable = 5,
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today),
            Status = ListingStatus.Active
        };
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateProductCommandHandler(_unitOfWork);
        var command = new UpdateProductCommand(
            OwnerId: _ownerId,
            ProductId: product.Id,
            CategoryId: null,
            Title: "New Title",
            TitleAr: null,
            Description: null,
            DescriptionAr: null,
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
            StoreId = _storeId,
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

        var uploadHandler = new UploadProductImageCommandHandler(_unitOfWork, mockFileStorage.Object);
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
        var deleteHandler = new DeleteProductImageCommandHandler(_unitOfWork);
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
}
