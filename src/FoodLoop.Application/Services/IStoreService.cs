using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Stores;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.Services;

/// <summary>
/// Backs the business onboarding wizard (business_signup_step_1 → business_verification_location
/// → document_upload_step_2 → verification_pending_step_3 UI screens). Registration (step 1)
/// is handled by IAuthService, which creates the draft Store; this service covers steps 2-3.
/// </summary>
public interface IStoreService
{
    /// <summary>GET /stores/me — the calling merchant's own store, including uploaded documents.</summary>
    Task<StoreDto> GetMyStoreAsync(Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>PATCH /stores/me/location — step 2's location fields.</summary>
    Task<StoreDto> UpdateLocationAsync(Guid ownerId, UpdateStoreLocationRequest request, CancellationToken cancellationToken = default);

    /// <summary>POST /stores/me/documents — step 2's document upload (called once per document type).</summary>
    Task<StoreDto> UploadDocumentAsync(Guid ownerId, UploadDocumentType verificationType, FileUploadRequest file, CancellationToken cancellationToken = default);
}
