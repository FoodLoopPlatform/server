using FoodLoop.API.Common;
using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.DTOs.Orders;
using FoodLoop.Application.Features.Organizations.Commands;
using FoodLoop.Application.Features.Organizations.Queries;
using FoodLoop.Application.Features.Orders.Commands;
using FoodLoop.Application.Features.Orders.Queries;
using FoodLoop.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace FoodLoop.API.Controllers;

/// <summary>
/// Backs the business onboarding wizard: business_signup_step_1 (registration, see
/// AuthController) â†’ business_verification_location (step 2 location, below) â†’
/// document_upload_step_2 (step 2 documents, below) â†’ verification_pending_step_3
/// (status, below). Full Organization CRUD (browsing, editing a live organization, etc.) ships in Sprint 2 â€”
/// only the merchant's own draft organization is exposed here.
/// </summary>
[ApiController]
[Route("stores")]
[Authorize(Roles = AppRole.Merchant)]
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

    /// <summary>GET /organizations/me â€” the caller's own organization, its location, and uploaded documents.
    /// Used to re-enter the wizard at the right step, and by verification_pending_step_3 to
    /// show current status.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyStore(CancellationToken cancellationToken)
    {
        var organization = await _mediator.Send(new GetMyOrganizationQuery(OwnerId), cancellationToken);
        return Ok(ApiResponse<OrganizationDto>.Ok(organization));
    }

    /// <summary>PATCH /organizations/me — updates the organization's name, description, category, logo, and cover photo (Form Data).</summary>
    [HttpPatch("me")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateProfile([FromForm] UpdateStoreProfileFormRequest request, CancellationToken cancellationToken)
    {
        FileUploadRequest? logoUpload = null;
        if (request.Logo != null && request.Logo.Length > 0)
        {
            var ext = Path.GetExtension(request.Logo.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            if (!allowedExtensions.Contains(ext))
            {
                return BadRequest(ApiResponse.Fail(_loc["InvalidImageFormat"]));
            }

            logoUpload = new FileUploadRequest
            {
                Content = request.Logo.OpenReadStream(),
                FileName = request.Logo.FileName,
                ContentType = request.Logo.ContentType
            };
        }

        FileUploadRequest? coverPhotoUpload = null;
        if (request.CoverPhoto != null && request.CoverPhoto.Length > 0)
        {
            var ext = Path.GetExtension(request.CoverPhoto.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            if (!allowedExtensions.Contains(ext))
            {
                return BadRequest(ApiResponse.Fail(_loc["InvalidImageFormat"]));
            }

            coverPhotoUpload = new FileUploadRequest
            {
                Content = request.CoverPhoto.OpenReadStream(),
                FileName = request.CoverPhoto.FileName,
                ContentType = request.CoverPhoto.ContentType
            };
        }

        if (!string.IsNullOrWhiteSpace(request.OpeningHours))
        {
            try
            {
                System.Text.Json.JsonDocument.Parse(request.OpeningHours);
            }
            catch (System.Text.Json.JsonException)
            {
                return BadRequest(ApiResponse.Fail(_loc["InvalidOpeningHoursJson"]));
            }
        }

        var appRequest = new UpdateOrganizationProfileRequest
        {
            Name = request.Name,
            Description = request.Description,
            BusinessCategory = request.BusinessCategory,
            LogoFile = logoUpload,
            CoverPhotoFile = coverPhotoUpload,
            Phone = request.Phone,
            Email = request.Email,
            OpeningHours = request.OpeningHours
        };

        var organization = await _mediator.Send(new UpdateOrganizationProfileCommand(OwnerId, appRequest), cancellationToken);
        return Ok(ApiResponse<OrganizationDto>.Ok(organization));
    }

    /// <summary>PATCH /organizations/me/location — step 2's location fields (business_verification_location).</summary>
    [HttpPatch("me/location")]
    public async Task<IActionResult> UpdateLocation([FromBody] UpdateOrganizationLocationRequest request, CancellationToken cancellationToken)
    {
        var organization = await _mediator.Send(new UpdateOrganizationLocationCommand(OwnerId, request), cancellationToken);
        return Ok(ApiResponse<OrganizationDto>.Ok(organization));
    }

    /// <summary>POST /organizations/me/documents â€” step 2's document upload (document_upload_step_2).
    /// Does not require authentication: the organization is identified by the owner's registered email.
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

        var ext = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExtensions.Contains(ext))
        {
            return BadRequest(ApiResponse.Fail(_loc["InvalidDocumentFormat"]));
        }

        await using var stream = request.File.OpenReadStream();
        var uploadRequest = new FileUploadRequest
        {
            Content = stream,
            FileName = request.File.FileName,
            ContentType = request.File.ContentType,
        };

        var organization = await _mediator.Send(new UploadOrganizationDocumentCommand(request.Email, request.Type, uploadRequest), cancellationToken);
        return Ok(ApiResponse<OrganizationDto>.Ok(organization));
    }

    /// <summary>
    /// GET /stores/me/orders — retrieve all orders placed to this store.
    /// </summary>
    [HttpGet("me/orders")]
    public async Task<IActionResult> GetReceivedOrders(CancellationToken cancellationToken)
    {
        var query = new GetMerchantOrdersQuery(OwnerId);
        var orders = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<OrderDto>>.Ok(orders));
    }

    /// <summary>
    /// PATCH /stores/me/orders/{id}/status — update order preparation or pickup status.
    /// </summary>
    [HttpPatch("me/orders/{id:guid}/status")]
    public async Task<IActionResult> UpdateOrderStatus(Guid id, [FromBody] UpdateOrderStatusRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateOrderStatusCommand(OwnerId, id, request.Status);
        var result = await _mediator.Send(command, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(ApiResponse.Fail(result.Message ?? "Failed to update order status"));
        }

        return Ok(ApiResponse<OrderDto>.Ok(result.Data!));
    }

    /// <summary>
    /// GET /stores/me/analytics — retrieve store-level analytics/metrics for the insights dashboard.
    /// Query param: period = today | week | month | all (default: all)
    /// </summary>
    [HttpGet("me/analytics")]
    public async Task<IActionResult> GetMyStoreAnalytics(
        [FromQuery] string period = "all",
        CancellationToken cancellationToken = default)
    {
        var allowed = new[] { "today", "week", "month", "all" };
        if (!allowed.Contains(period.ToLowerInvariant()))
            return BadRequest(ApiResponse.Fail("Invalid period. Allowed values: today, week, month, all."));

        var query = new GetStoreAnalyticsQuery(OwnerId, period);
        var analytics = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<StoreAnalyticsDto>.Ok(analytics));
    }

    /// <summary>
    /// GET /stores/me/ai-settings — read AI automation preferences.
    /// </summary>
    [HttpGet("me/ai-settings")]
    public async Task<IActionResult> GetAiSettings(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAiSettingsQuery(OwnerId), cancellationToken);
        return Ok(ApiResponse<AiSettingsDto>.Ok(result));
    }

    /// <summary>
    /// PATCH /stores/me/ai-settings — update AI automation preferences.
    /// </summary>
    [HttpPatch("me/ai-settings")]
    public async Task<IActionResult> UpdateAiSettings([FromBody] UpdateAiSettingsRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateAiSettingsCommand(
            OwnerId,
            request.AiAutoDiscountEnabled,
            request.AiAutoDiscountPercent,
            request.AiAutoDiscountDaysBeforeExpiry,
            request.AiAutoPricingEnabled);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<AiSettingsDto>.Ok(result));
    }

    /// <summary>
    /// GET /stores/me/delivery/fleet — active orders overview for fleet/logistics management.
    /// </summary>
    [HttpGet("me/delivery/fleet")]
    public async Task<IActionResult> GetDeliveryFleet(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetDeliveryFleetQuery(OwnerId), cancellationToken);
        return Ok(ApiResponse<DeliveryFleetDto>.Ok(result));
    }

    /// <summary>
    /// POST /stores/me/donations — donate surplus product inventory to a verified charity.
    /// </summary>
    [HttpPost("me/donations")]
    public async Task<IActionResult> DonateSurplus([FromBody] DonateSurplusRequest request, CancellationToken cancellationToken)
    {
        var command = new DonateSurplusCommand(OwnerId, request.RecipientOrganizationId, request.ProductId, request.Quantity, request.Note);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<DonationDto>.Ok(result));
    }
}

public class UploadStoreDocumentRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = null!;

    /// <summary>Document type: CommercialRegistration | TaxIdCertificate | StoreFacilityPhoto</summary>
    [Required]
    public UploadDocumentType Type { get; set; }

    [Required]
    public IFormFile File { get; set; } = null!;
}

public class UpdateStoreProfileFormRequest
{
    [MaxLength(150)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    public BusinessCategory? BusinessCategory { get; set; }

    public IFormFile? Logo { get; set; }

    public IFormFile? CoverPhoto { get; set; }

    [Phone, MaxLength(20)]
    public string? Phone { get; set; }

    [EmailAddress, MaxLength(256)]
    public string? Email { get; set; }

    public string? OpeningHours { get; set; }
}

public class UpdateOrderStatusRequest
{
    [Required]
    public string Status { get; set; } = null!;
}

public class UpdateAiSettingsRequest
{
    public bool AiAutoDiscountEnabled { get; set; }
    [Range(0, 100)] public int AiAutoDiscountPercent { get; set; } = 20;
    [Range(1, 365)] public int AiAutoDiscountDaysBeforeExpiry { get; set; } = 3;
    public bool AiAutoPricingEnabled { get; set; }
}

public class DonateSurplusRequest
{
    [Required] public Guid RecipientOrganizationId { get; set; }
    [Required] public Guid ProductId { get; set; }
    [Required, Range(1, 100000)] public int Quantity { get; set; }
    public string? Note { get; set; }
}

