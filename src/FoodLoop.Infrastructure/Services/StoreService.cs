using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Stores;
using FoodLoop.Application.Services;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Services;

public class StoreService : IStoreService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IFileStorageService _fileStorage;

    public StoreService(IApplicationDbContext dbContext, IFileStorageService fileStorage)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
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

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(store);
    }

    public async Task<StoreDto> UploadDocumentAsync(Guid ownerId, string verificationType, FileUploadRequest file, CancellationToken cancellationToken = default)
    {
        if (!DocumentTypes.All.Contains(verificationType))
        {
            throw new ArgumentException(
                $"Unknown document type '{verificationType}'. Expected one of: {string.Join(", ", DocumentTypes.All)}.");
        }

        var store = await FindStoreOrThrowAsync(ownerId, cancellationToken);

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
            _dbContext.StoreVerifications.Add(new StoreVerification
            {
                StoreId = store.Id,
                VerificationType = verificationType,
                DocumentUrl = documentUrl,
                Status = VerificationStatus.Pending,
            });
        }

        // Once all three required documents are in, the store moves from Unverified
        // to Pending admin review (matches verification_pending_step_3).
        if (DocumentTypes.All.All(t => store.Verifications.Any(v => v.VerificationType == t)))
        {
            store.VerificationStatus = VerificationStatus.Pending;
        }

        store.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(store);
    }

    private async Task<Store> FindStoreOrThrowAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        var store = await _dbContext.Stores
            .Include(s => s.Verifications)
            .FirstOrDefaultAsync(s => s.OwnerId == ownerId, cancellationToken);

        return store ?? throw new NotFoundException(
            "No store was found for this account. Business accounts get a draft store automatically at registration.");
    }

    private static StoreDto ToDto(Store store) => new()
    {
        Id = store.Id,
        Name = store.Name,
        StoreType = store.StoreType,
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
            VerificationType = v.VerificationType,
            DocumentUrl = v.DocumentUrl,
            Status = v.Status.ToString(),
        }).ToArray(),
    };
}
