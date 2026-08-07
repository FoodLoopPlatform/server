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

    /// <summary>PATCH /organizations/me â€” updates the organization's name, description, category, and logo (Form Data).</summary>
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
            NameAr = request.NameAr,
            Description = request.Description,
            DescriptionAr = request.DescriptionAr,
            BusinessCategory = request.BusinessCategory,
            LogoFile = logoUpload,
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

    [MaxLength(150)]
    public string? NameAr { get; set; }

    public string? Description { get; set; }

    public string? DescriptionAr { get; set; }

    public BusinessCategory? BusinessCategory { get; set; }

    public IFormFile? Logo { get; set; }

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

