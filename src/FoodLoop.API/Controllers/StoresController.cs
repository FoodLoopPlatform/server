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
using System.ComponentModel.DataAnnotations;

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
[Authorize(Roles = $"{AppRole.Merchant},{AppRole.Charity}")]
public class StoresController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalizationService _loc;

    public StoresController(ISender mediator, ICurrentUserService currentUser, ILocalizationService loc)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _loc = loc;
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

    /// <summary>PATCH /stores/me — updates the store's name, description, category, and logo.</summary>
    [HttpPatch("me")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateStoreProfileRequest request, CancellationToken cancellationToken)
    {
        var store = await _mediator.Send(new UpdateStoreProfileCommand(OwnerId, request), cancellationToken);
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
            return BadRequest(ApiResponse.Fail(_loc["OwnerEmailRequired"]));
        }

        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(ApiResponse.Fail(_loc["FileRequired"]));
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
    [Required, EmailAddress]
    public string Email { get; set; } = null!;

    /// <summary>One of: CommercialRegistration | TaxIdCertificate | StoreFacilityPhoto</summary>
    [Required]
    public string Type { get; set; } = null!;

    [Required]
    public IFormFile File { get; set; } = null!;
}
