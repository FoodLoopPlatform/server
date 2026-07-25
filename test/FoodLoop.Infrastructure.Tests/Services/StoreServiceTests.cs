using FluentAssertions;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Stores;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Services;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Moq;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Services;

public class StoreServiceTests
{
    private readonly Mock<IFileStorageService> _fileStorage = new();

    private StoreService CreateService(IApplicationDbContext dbContext) =>
        new(dbContext, _fileStorage.Object);

    [Fact]
    public async Task GetMyStoreAsync_should_throw_NotFound_when_owner_has_no_store()
    {
        await using var dbContext = ApplicationDbContextFactory.Create();
        var service = CreateService(dbContext);

        var act = () => service.GetMyStoreAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetMyStoreAsync_should_return_the_owners_store_as_a_dto()
    {
        await using var dbContext = ApplicationDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        dbContext.Stores.Add(new Store { OwnerId = ownerId, Name = "Nile Grocer", StoreType = StoreType.Standard });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetMyStoreAsync(ownerId);

        result.Name.Should().Be("Nile Grocer");
        result.VerificationStatus.Should().Be(VerificationStatus.Unverified.ToString());
    }

    [Fact]
    public async Task UpdateLocationAsync_should_persist_the_new_location_fields()
    {
        await using var dbContext = ApplicationDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        dbContext.Stores.Add(new Store { OwnerId = ownerId, Name = "Nile Grocer" });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var request = new UpdateStoreLocationRequest
        {
            Governorate = "Cairo",
            City = "Nasr City",
            Neighborhood = "7th District",
            Street = "Makram Ebeid St.",
            Latitude = 30.05,
            Longitude = 31.34,
        };

        var result = await service.UpdateLocationAsync(ownerId, request);

        result.Governorate.Should().Be("Cairo");
        result.City.Should().Be("Nasr City");
        result.Latitude.Should().Be(30.05);
        result.Longitude.Should().Be(31.34);
    }

    [Fact]
    public async Task UploadDocumentAsync_should_reject_an_unknown_document_type()
    {
        await using var dbContext = ApplicationDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        dbContext.Stores.Add(new Store { OwnerId = ownerId, Name = "Nile Grocer" });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var file = new FileUploadRequest { Content = new MemoryStream(), FileName = "doc.pdf", ContentType = "application/pdf" };

        var act = () => service.UploadDocumentAsync(ownerId, "NotARealDocumentType", file);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UploadDocumentAsync_should_replace_an_existing_upload_of_the_same_type_instead_of_duplicating()
    {
        await using var dbContext = ApplicationDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var store = new Store { OwnerId = ownerId, Name = "Nile Grocer" };
        store.Verifications.Add(new StoreVerification
        {
            StoreId = store.Id,
            VerificationType = DocumentTypes.CommercialRegistration,
            DocumentUrl = "old-url",
            Status = VerificationStatus.Verified,
        });
        dbContext.Stores.Add(store);
        await dbContext.SaveChangesAsync();

        _fileStorage
            .Setup(f => f.SaveAsync(It.IsAny<FileUploadRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-url");

        var service = CreateService(dbContext);
        var file = new FileUploadRequest { Content = new MemoryStream(), FileName = "doc.pdf", ContentType = "application/pdf" };

        var result = await service.UploadDocumentAsync(ownerId, DocumentTypes.CommercialRegistration, file);

        result.Documents.Should().HaveCount(1);
        result.Documents.Single().DocumentUrl.Should().Be("new-url");
        result.Documents.Single().Status.Should().Be(VerificationStatus.Pending.ToString());
    }

    [Fact]
    public async Task UploadDocumentAsync_should_move_store_to_Pending_once_all_required_documents_are_present()
    {
        await using var dbContext = ApplicationDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var store = new Store { OwnerId = ownerId, Name = "Nile Grocer", VerificationStatus = VerificationStatus.Unverified };
        store.Verifications.Add(new StoreVerification { StoreId = store.Id, VerificationType = DocumentTypes.CommercialRegistration, DocumentUrl = "u1" });
        store.Verifications.Add(new StoreVerification { StoreId = store.Id, VerificationType = DocumentTypes.TaxIdCertificate, DocumentUrl = "u2" });
        dbContext.Stores.Add(store);
        await dbContext.SaveChangesAsync();

        _fileStorage
            .Setup(f => f.SaveAsync(It.IsAny<FileUploadRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("u3");

        var service = CreateService(dbContext);
        var file = new FileUploadRequest { Content = new MemoryStream(), FileName = "photo.jpg", ContentType = "image/jpeg" };

        // The third and final required document type triggers the Unverified -> Pending move.
        var result = await service.UploadDocumentAsync(ownerId, DocumentTypes.StoreFacilityPhoto, file);

        result.Documents.Should().HaveCount(3);
        result.VerificationStatus.Should().Be(VerificationStatus.Pending.ToString());
    }
}
