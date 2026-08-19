using FluentAssertions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Products;
using FoodLoop.Infrastructure.Features.Products.Commands;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Products;

public class ProductCsvFuzzingAndValidationTests : IDisposable
{
    private readonly ApplicationDbContext _db = ApplicationDbContextFactory.Create();
    private readonly UnitOfWork _unitOfWork;
    private readonly Mock<IAuditLogService> _mockAudit = new();
    private readonly Mock<IRealTimeNotificationService> _mockNotification = new();
    private readonly Mock<ILogger<BulkUploadProductsCommandHandler>> _mockLogger = new();

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public ProductCsvFuzzingAndValidationTests()
    {
        _unitOfWork = new UnitOfWork(_db);

        var owner = new ApplicationUser
        {
            Id = _ownerId,
            UserName = "merchant@csvtest.com",
            Email = "merchant@csvtest.com",
            Status = UserStatus.Active
        };
        _db.Users.Add(owner);

        var org = new Organization
        {
            Id = _orgId,
            OwnerId = _ownerId,
            Name = "CSV Gourmet",
            VerificationStatus = VerificationStatus.Verified
        };
        _db.Organizations.Add(org);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Dairy",
            NameAr = "منتجات الألبان"
        };
        _db.Categories.Add(category);

        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private BulkUploadProductsCommandHandler CreateHandler()
    {
        return new BulkUploadProductsCommandHandler(
            _unitOfWork,
            _mockAudit.Object,
            _mockNotification.Object,
            _mockLogger.Object);
    }

    private static FileUploadRequest CreateCsvRequest(string csvContent)
    {
        return new FileUploadRequest
        {
            Content = new MemoryStream(Encoding.UTF8.GetBytes(csvContent)),
            FileName = "upload.csv",
            ContentType = "text/csv"
        };
    }

    [Fact(DisplayName = "TC-CSV-01: Empty CSV throws ArgumentException")]
    public async Task BulkUpload_EmptyCsv_ThrowsArgumentException()
    {
        var handler = CreateHandler();
        var command = new BulkUploadProductsCommand(_ownerId, CreateCsvRequest(""));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("The uploaded CSV file is empty.");
    }

    [Fact(DisplayName = "TC-CSV-02: Missing required header throws ArgumentException")]
    public async Task BulkUpload_MissingRequiredHeader_ThrowsArgumentException()
    {
        var handler = CreateHandler();
        // Missing 'discountedprice'
        var csv = "title,originalprice,quantityavailable,expirationdate,categoryname\nMilk,20.00,5,2026-12-31,Dairy";
        var command = new BulkUploadProductsCommand(_ownerId, CreateCsvRequest(csv));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Missing required CSV header: 'discountedprice'");
    }

    [Fact(DisplayName = "TC-CSV-03: Row with DiscountedPrice greater than OriginalPrice throws ArgumentException")]
    public async Task BulkUpload_DiscountGreaterThanOriginal_ThrowsArgumentException()
    {
        var handler = CreateHandler();
        var csv = "title,originalprice,discountedprice,quantityavailable,expirationdate,categoryname\n" +
                  "Premium Butter,30.00,50.00,5,2026-12-31,Dairy";
        var command = new BulkUploadProductsCommand(_ownerId, CreateCsvRequest(csv));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Row 2: DiscountedPrice cannot be greater than OriginalPrice.");
    }

    [Fact(DisplayName = "TC-CSV-04: Row with negative price throws ArgumentException")]
    public async Task BulkUpload_NegativePrice_ThrowsArgumentException()
    {
        var handler = CreateHandler();
        var csv = "title,originalprice,discountedprice,quantityavailable,expirationdate,categoryname\n" +
                  "Bad Price Item,-10.00,5.00,5,2026-12-31,Dairy";
        var command = new BulkUploadProductsCommand(_ownerId, CreateCsvRequest(csv));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Row 2: Invalid or negative OriginalPrice '-10.00'.");
    }

    [Fact(DisplayName = "TC-CSV-05: Row with invalid date format throws ArgumentException")]
    public async Task BulkUpload_InvalidDateFormat_ThrowsArgumentException()
    {
        var handler = CreateHandler();
        var csv = "title,originalprice,discountedprice,quantityavailable,expirationdate,categoryname\n" +
                  "Malformed Date Item,20.00,10.00,5,31/12/2026,Dairy"; // Not ISO YYYY-MM-DD
        var command = new BulkUploadProductsCommand(_ownerId, CreateCsvRequest(csv));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Row 2: Invalid ExpirationDate format '31/12/2026'. Use YYYY-MM-DD.");
    }

    [Fact(DisplayName = "TC-CSV-06: Non-existent category name throws ArgumentException")]
    public async Task BulkUpload_NonExistentCategory_ThrowsArgumentException()
    {
        var handler = CreateHandler();
        var csv = "title,originalprice,discountedprice,quantityavailable,expirationdate,categoryname\n" +
                  "Mysterious Item,20.00,10.00,5,2026-12-31,Electronics"; // Electronics doesn't exist
        var command = new BulkUploadProductsCommand(_ownerId, CreateCsvRequest(csv));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Row 2: Category 'Electronics' not found.");
    }

    [Fact(DisplayName = "TC-CSV-07: Arabic category name matches correctly")]
    public async Task BulkUpload_ArabicCategoryName_MatchesCategorySuccessfully()
    {
        var handler = CreateHandler();
        var csv = "title,originalprice,discountedprice,quantityavailable,expirationdate,categoryname\n" +
                  "Labneh,40.00,25.00,10,2026-12-31,منتجات الألبان";
        var command = new BulkUploadProductsCommand(_ownerId, CreateCsvRequest(csv));

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Labneh");
        result[0].CategoryName.Should().Be("Dairy");
    }
}
