using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Stores;
using FoodLoop.Application.Services;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using System.Linq;

namespace FoodLoop.Infrastructure.Services;

public class StoreService : IStoreService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly UserManager<ApplicationUser> _userManager;

    public StoreService(IUnitOfWork unitOfWork, IFileStorageService fileStorage, UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _userManager = userManager;
    }

    public async Task<StoreDto> GetMyStoreAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var store = await FindStoreOrThrowAsync(ownerId, cancellationToken);
        return ToDto(store);
    }

    public async Task<StoreDto> UpdateLocationAsync(Guid ownerId, UpdateStoreLocationRequest request, CancellationToken cancellationToken = default)
    {
        var store = await FindStoreOrThrowAsync(ownerId, cancellationToken);

        store.Governorate = request.Governorate;
        store.City = request.City;
        store.Neighborhood = request.Neighborhood;
        store.Street = request.Street;
        store.Latitude = request.Latitude;
        store.Longitude = request.Longitude;
        store.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(store);
    }

    public async Task<StoreDto> UploadDocumentAsync(Guid ownerId, UploadDocumentType verificationType, FileUploadRequest file, CancellationToken cancellationToken = default)
    {
        var store = await FindStoreOrThrowAsync(ownerId, cancellationToken);

        var owner = await _userManager.FindByIdAsync(store.OwnerId.ToString());
        if (owner == null)
        {
            throw new NotFoundException("Owner user not found.");
        }

        var isCharity = await _userManager.IsInRoleAsync(owner, AppRole.Charity);

        // Validate allowed document types based on role
        if (isCharity)
        {
            var allowedCharityTypes = new[] { UploadDocumentType.AssociationCertificate, UploadDocumentType.CharityBylaws, UploadDocumentType.BoardOfDirectorsList };
            if (!allowedCharityTypes.Contains(verificationType))
            {
                throw new ArgumentException("Charities can only upload AssociationCertificate, CharityBylaws, or BoardOfDirectorsList.");
            }
        }
        else
        {
            var allowedMerchantTypes = new[] { UploadDocumentType.CommercialRegistration, UploadDocumentType.TaxIdCertificate, UploadDocumentType.StoreFacilityPhoto };
            if (!allowedMerchantTypes.Contains(verificationType))
            {
                throw new ArgumentException("Stores can only upload CommercialRegistration, TaxIdCertificate, or StoreFacilityPhoto.");
            }
        }

        var documentUrl = await _fileStorage.SaveAsync(file, $"stores/{store.Id}", cancellationToken);

        // Replace any prior upload of the same type rather than accumulating duplicates.
        var existing = store.Verifications.FirstOrDefault(v => v.VerificationType == verificationType);
        if (existing != null)
        {
            existing.DocumentUrl = documentUrl;
            existing.Status = VerificationStatus.Pending;
            existing.ReviewedAt = null;
            existing.ReviewedBy = null;
        }
        else
        {
            var storeVerification = new StoreVerification
            {
                StoreId = store.Id,
                VerificationType = verificationType,
                DocumentUrl = documentUrl,
                Status = VerificationStatus.Pending,
            };
            _unitOfWork.Repository<StoreVerification>().Add(storeVerification);
            if (!store.Verifications.Contains(storeVerification))
            {
                store.Verifications.Add(storeVerification);
            }
        }

        // Determine required document types
        var requiredTypes = isCharity
            ? new[] { UploadDocumentType.AssociationCertificate, UploadDocumentType.CharityBylaws, UploadDocumentType.BoardOfDirectorsList }
            : new[] { UploadDocumentType.CommercialRegistration, UploadDocumentType.TaxIdCertificate, UploadDocumentType.StoreFacilityPhoto };

        if (requiredTypes.All(t => store.Verifications.Any(v => v.VerificationType == t)))
        {
            store.VerificationStatus = VerificationStatus.Pending;
        }

        store.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(store);
    }

    private async Task<Store> FindStoreOrThrowAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        var store = await _unitOfWork.Stores.GetByOwnerIdAsync(ownerId, cancellationToken);

        return store ?? throw new NotFoundException(
            "No store was found for this account. Business accounts get a draft store automatically at registration.");
    }

    private static StoreDto ToDto(Store store) => new()
    {
        Id = store.Id,
        Name = store.Name,
        BusinessCategory = store.BusinessCategory,
        Governorate = store.Governorate,
        City = store.City,
        Neighborhood = store.Neighborhood,
        Street = store.Street,
        Latitude = store.Latitude,
        Longitude = store.Longitude,
        VerificationStatus = store.VerificationStatus.ToString(),
        Documents = store.Verifications.Select(v => new StoreDocumentDto
        {
            Id = v.Id,
            VerificationType = v.VerificationType.ToString(),
            DocumentUrl = v.DocumentUrl,
            Status = v.Status.ToString(),
        }).ToArray(),
    };
}
