using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.Services;

/// <summary>
/// Backs the business onboarding wizard (business_signup_step_1 â†’ business_verification_location
/// â†’ document_upload_step_2 â†’ verification_pending_step_3 UI screens). Registration (step 1)
/// is handled by IAuthService, which creates the draft Organization; this service covers steps 2-3.
/// </summary>
public interface IStoreService
{
    /// <summary>GET /organizations/me â€” the calling merchant's own organization, including uploaded documents.</summary>
    Task<OrganizationDto> GetMyStoreAsync(Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>PATCH /organizations/me/location â€” step 2's location fields.</summary>
    Task<OrganizationDto> UpdateLocationAsync(Guid ownerId, UpdateStoreLocationRequest request, CancellationToken cancellationToken = default);

    /// <summary>POST /organizations/me/documents â€” step 2's document upload (called once per document type).</summary>
    Task<OrganizationDto> UploadDocumentAsync(Guid ownerId, UploadDocumentType verificationType, FileUploadRequest file, CancellationToken cancellationToken = default);
}

