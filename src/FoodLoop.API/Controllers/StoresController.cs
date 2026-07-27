using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Stores;
using FoodLoop.Application.Features.Stores.Commands;
using FoodLoop.Application.Features.Stores.Queries;
using FoodLoop.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.API.Controllers;

/// <summary>
/// Backs the business onboarding wizard: business_signup_step_1 (registration, see
/// AuthController) → business_verification_location (step 2 location, below) →
/// document_upload_step_2 (step 2 documents, below) → verification_pending_step_3
/// (status, below). Full Store CRUD (browsing, editing a live store, etc.) ships in Sprint 2 —
/// only the merchant's own draft store is exposed here.
/// </summary>
[ApiController]
[Route("stores")]
[Authorize(Roles = AppRole.Merchant)]
public class StoresController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICurrentUserService _currentUser;

    public StoresController(ISender mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    private Guid OwnerId => _currentUser.UserId ?? throw new UnauthorizedAccessException();

    /// <summary>GET /stores/me — the caller's own store, its location, and uploaded documents.
    /// Used to re-enter the wizard at the right step, and by verification_pending_step_3 to
    /// show current status.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyStore(CancellationToken cancellationToken)
    {
        var store = await _mediator.Send(new GetMyStoreQuery(OwnerId), cancellationToken);
        return Ok(ApiResponse<StoreDto>.Ok(store));
    }

    /// <summary>PATCH /stores/me/location — step 2's location fields (business_verification_location).</summary>
    [HttpPatch("me/location")]
    public async Task<IActionResult> UpdateLocation([FromBody] UpdateStoreLocationRequest request, CancellationToken cancellationToken)
    {
        var store = await _mediator.Send(new UpdateStoreLocationCommand(OwnerId, request), cancellationToken);
        return Ok(ApiResponse<StoreDto>.Ok(store));
    }

    /// <summary>POST /stores/me/documents — step 2's document upload (document_upload_step_2).
    /// Does not require authentication: the store is identified by the owner's registered email.
    /// Call once per slot with type = CommercialRegistration | TaxIdCertificate | StoreFacilityPhoto.</summary>
    [HttpPost("me/documents")]
    [AllowAnonymous]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadDocument([FromForm] UploadStoreDocumentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(ApiResponse.Fail("The owner email is required to link documents to an account."));
        }

        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(ApiResponse.Fail("A file is required."));
        }

        await using var stream = request.File.OpenReadStream();
        var uploadRequest = new FileUploadRequest
        {
            Content = stream,
            FileName = request.File.FileName,
            ContentType = request.File.ContentType,
        };

        var store = await _mediator.Send(new UploadStoreDocumentCommand(request.Email, request.Type, uploadRequest), cancellationToken);
        return Ok(ApiResponse<StoreDto>.Ok(store));
    }
}

public class UploadStoreDocumentRequest
{
    public string Email { get; set; } = null!;
    public string Type { get; set; } = null!;
    public IFormFile File { get; set; } = null!;
}
