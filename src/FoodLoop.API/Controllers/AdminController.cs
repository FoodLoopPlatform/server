using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Application.Features.Users.Queries;
using FoodLoop.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.API.Controllers;

[ApiController]
[Route("admin")]
[Authorize(Roles = AppRole.Admin)]
public class AdminController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICurrentUserService _currentUser;

    public AdminController(ISender mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    private Guid AdminId => _currentUser.UserId ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// GET /admin/organizations/pending â€” lists all organizations awaiting verification review.
    /// Accessible without auth so the admin frontend can display the queue.
    /// Each entry includes the owner's contact details and all uploaded documents.
    /// </summary>
    [HttpGet("stores/pending")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPendingStores(CancellationToken cancellationToken)
    {
        var organizations = await _mediator.Send(new GetPendingStoresQuery(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminOrganizationDto>>.Ok(organizations));
    }

    /// <summary>
    /// GET /admin/organizations/{id} â€” full organization detail with all documents for a single review.
    /// Accessible without auth so the admin frontend can deep-link to a specific review.
    /// </summary>
    [HttpGet("stores/{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStoreForReview(Guid id, CancellationToken cancellationToken)
    {
        var organization = await _mediator.Send(new GetStoreForReviewQuery(id), cancellationToken);
        return Ok(ApiResponse<AdminOrganizationDto>.Ok(organization));
    }

    /// <summary>
    /// PATCH /admin/organizations/{id}/verify â€” approve or reject a organization.
    /// Action must be "Approved" or "Rejected".
    /// On approval the owner's account is activated; on rejection it stays PendingVerification
    /// so they can correct and re-submit.
    /// </summary>
    [HttpPatch("stores/{id:guid}/verify")]
    public async Task<IActionResult> VerifyStore(Guid id, [FromBody] VerifyOrganizationRequest request, CancellationToken cancellationToken)
    {
        var organization = await _mediator.Send(new VerifyOrganizationCommand(id, AdminId, request), cancellationToken);
        return Ok(ApiResponse<AdminOrganizationDto>.Ok(organization));
    }

    /// <summary>
    /// PATCH /admin/charities/{id}/verify â€” approve or reject a charity's onboarding verification.
    /// Action must be "Approved" or "Rejected".
    /// </summary>
    [HttpPatch("charities/{id:guid}/verify")]
    public async Task<IActionResult> VerifyCharity(Guid id, [FromBody] VerifyOrganizationRequest request, CancellationToken cancellationToken)
    {
        var organization = await _mediator.Send(new VerifyOrganizationCommand(id, AdminId, request), cancellationToken);
        return Ok(ApiResponse<AdminOrganizationDto>.Ok(organization));
    }


    /// <summary>
    /// GET /admin/users â€” lists all users with optional filtering.
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> ListUsers(
        [FromQuery] string? role,
        [FromQuery] string? status,
        [FromQuery] string? searchTerm,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListUsersQuery(role, status, searchTerm, page, pageSize), cancellationToken);
        return Ok(ApiResponse<PagedResult<UserDto>>.Ok(result));
    }

    /// <summary>
    /// PATCH /admin/users/{id}/status â€” suspend, ban, or reactivate a user.
    /// Body: { "status": "Active" | "Suspended" | "Banned" }
    /// </summary>
    [HttpPatch("users/{id:guid}/status")]
    public async Task<IActionResult> UpdateUserStatus(
        Guid id, [FromBody] UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateUserStatusCommand(id, request), cancellationToken);
        if (!result.Success)
            return BadRequest(ApiResponse.Fail(result.Message!, result.Errors));
        return Ok(ApiResponse<UserDto>.Ok(result.Data!));
    }

    /// <summary>
    /// GET /admin/users/{id}/activity-log â€” recent events for a user (account created, orders placed, support tickets).
    /// </summary>
    [HttpGet("users/{id:guid}/activity-log")]
    public async Task<IActionResult> GetUserActivityLog(Guid id, CancellationToken cancellationToken)
    {
        var log = await _mediator.Send(new GetUserActivityLogQuery(id), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ActivityLogEntryDto>>.Ok(log));
    }

    /// <summary>
    /// GET /admin/organizations/{id}/activity-log â€” recent events for a organization (uploads, Products, reviews, orders, tickets).
    /// </summary>
    [HttpGet("stores/{id:guid}/activity-log")]
    public async Task<IActionResult> GetStoreActivityLog(Guid id, CancellationToken cancellationToken)
    {
        var log = await _mediator.Send(new GetStoreActivityLogQuery(id), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ActivityLogEntryDto>>.Ok(log));
    }

    /// <summary>
    /// GET /admin/charities/{id}/activity-log â€” recent events for a charity (uploads, verifications, tickets).
    /// </summary>
    [HttpGet("charities/{id:guid}/activity-log")]
    public async Task<IActionResult> GetCharityActivityLog(Guid id, CancellationToken cancellationToken)
    {
        var log = await _mediator.Send(new GetCharityActivityLogQuery(id), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ActivityLogEntryDto>>.Ok(log));
    }

    /// <summary>
    /// GET /admin/activity-logs/admin-actions — paginated feed of actions performed by admin users.
    /// Covers: DocumentVerified, UserStatusUpdated, DisputeResolved,
    ///         ProductModerated, ReviewModerated, SupportTicketClosed.
    /// Supports filtering by adminUserId, eventType, dateFrom, dateTo, and free-text searchTerm.
    /// </summary>
    [HttpGet("activity-logs/admin-actions")]
    public async Task<IActionResult> GetAdminActivityLogs(
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? eventType = null,
        [FromQuery] Guid? adminUserId = null,
        [FromQuery] DateTimeOffset? dateFrom = null,
        [FromQuery] DateTimeOffset? dateTo = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminActivityLogsQuery(
            searchTerm, eventType, adminUserId, dateFrom, dateTo, pageNumber, pageSize);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<AdminActivityLogsResultDto>.Ok(result));
    }

    /// <summary>
    /// GET /admin/activity-logs — global platform-wide activity and audit log feed with search & filtering.
    /// </summary>
    [HttpGet("activity-logs")]
    public async Task<IActionResult> GetPlatformActivityLogs(
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? eventType = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] Guid? organizationId = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var logs = await _mediator.Send(new GetPlatformActivityLogsQuery(searchTerm, eventType, userId, organizationId, pageNumber, pageSize), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ActivityLogEntryDto>>.Ok(logs));
    }

    /// <summary>
    /// GET /admin/activity-logs/{id} — get a specific audit log details for the details modal.
    /// </summary>
    [HttpGet("activity-logs/{id:guid}")]
    public async Task<IActionResult> GetActivityLogById(Guid id, CancellationToken cancellationToken)
    {
        var log = await _mediator.Send(new GetActivityLogByIdQuery(id), cancellationToken);
        return Ok(ApiResponse<ActivityLogEntryDto>.Ok(log));
    }

    // ── Analytics ─────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /admin/analytics/summary â€” high-level metrics for dashboard (total users, organizations, sales, savings).
    /// </summary>
    [HttpGet("analytics/summary")]
    public async Task<IActionResult> GetAnalyticsSummary(CancellationToken cancellationToken)
    {
        var summary = await _mediator.Send(new GetAnalyticsSummaryQuery(), cancellationToken);
        return Ok(ApiResponse<AnalyticsSummaryDto>.Ok(summary));
    }

    // â”€â”€ Organization moderation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// GET /admin/organizations â€” list all organizations with optional VerificationStatus filter.
    /// </summary>
    [HttpGet("stores")]
    public async Task<IActionResult> GetStores(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] VerificationStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var organizations = await _mediator.Send(new GetAdminStoresQuery(pageNumber, pageSize, status), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminOrganizationDto>>.Ok(organizations));
    }

    /// <summary>
    /// GET /admin/charities â€” list all charities with optional VerificationStatus filter.
    /// </summary>
    [HttpGet("charities")]
    public async Task<IActionResult> GetCharities(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] VerificationStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var charities = await _mediator.Send(new GetAdminCharitiesQuery(pageNumber, pageSize, status), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminOrganizationDto>>.Ok(charities));
    }

    // â”€â”€ Review moderation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// GET /admin/reviews â€” list all reviews with optional Rating and OrganizationId filters.
    /// </summary>
    [HttpGet("reviews")]
    public async Task<IActionResult> GetReviews(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? rating = null,
        [FromQuery] Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        var reviews = await _mediator.Send(new GetAdminReviewsQuery(pageNumber, pageSize, rating, organizationId), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminReviewDto>>.Ok(reviews));
    }

    /// <summary>
    /// DELETE /admin/reviews/{id} â€” moderate and remove an inappropriate review.
    /// </summary>
    [HttpDelete("reviews/{id:guid}")]
    public async Task<IActionResult> DeleteReview(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteReviewCommand(id), cancellationToken);
        return NoContent();
    }

    // â”€â”€ Product moderation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// GET /admin/products â€” list all products with optional Status and OrganizationId filters.
    /// </summary>
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        var products = await _mediator.Send(new GetAdminProductsQuery(pageNumber, pageSize, status, organizationId), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminProductDto>>.Ok(products));
    }

    /// <summary>
    /// DELETE /admin/products/{id} â€” suspend and soft-delete a product.
    /// </summary>
    [HttpDelete("products/{id:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new AdminDeleteProductCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// GET /admin/products/pending-ai â€” list pending products with low AI confidence score.
    /// </summary>
    [HttpGet("products/pending-ai")]
    public async Task<IActionResult> GetPendingLowConfProducts(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] double confidenceThreshold = 0.8,
        CancellationToken cancellationToken = default)
    {
        var products = await _mediator.Send(new GetPendingLowConfProductsQuery(pageNumber, pageSize, confidenceThreshold), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminProductDto>>.Ok(products));
    }

    /// <summary>
    /// PATCH /admin/products/{id}/approve â€” approve a product.
    /// </summary>
    [HttpPatch("products/{id:guid}/approve")]
    public async Task<IActionResult> ApproveProduct(Guid id, CancellationToken cancellationToken)
    {
        var product = await _mediator.Send(new ModerateProductCommand(id, "Approve", null), cancellationToken);
        return Ok(ApiResponse<AdminProductDto>.Ok(product));
    }

    /// <summary>
    /// PATCH /admin/products/{id}/reject â€” reject a product with a reason note.
    /// </summary>
    [HttpPatch("products/{id:guid}/reject")]
    public async Task<IActionResult> RejectProduct(Guid id, [FromBody] ProductModerationRequest request, CancellationToken cancellationToken)
    {
        var product = await _mediator.Send(new ModerateProductCommand(id, "Reject", request.Note), cancellationToken);
        return Ok(ApiResponse<AdminProductDto>.Ok(product));
    }

    /// <summary>
    /// PATCH /admin/products/{id}/request-changes â€” request changes for a product with instructions note.
    /// </summary>
    [HttpPatch("products/{id:guid}/request-changes")]
    public async Task<IActionResult> RequestChangesProduct(Guid id, [FromBody] ProductModerationRequest request, CancellationToken cancellationToken)
    {
        var product = await _mediator.Send(new ModerateProductCommand(id, "RequestChanges", request.Note), cancellationToken);
        return Ok(ApiResponse<AdminProductDto>.Ok(product));
    }

    // â”€â”€ Support Tickets â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// GET /admin/support-tickets â€” list support tickets with status and priority filters.
    /// </summary>
    [HttpGet("support-tickets")]
    public async Task<IActionResult> GetSupportTickets(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        CancellationToken cancellationToken = default)
    {
        var tickets = await _mediator.Send(new GetSupportTicketsQuery(pageNumber, pageSize, status, priority), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SupportTicketDto>>.Ok(tickets));
    }

    /// <summary>
    /// GET /admin/support-tickets/{id} â€” get a ticket detail with full conversation history.
    /// </summary>
    [HttpGet("support-tickets/{id:guid}")]
    public async Task<IActionResult> GetSupportTicketDetail(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await _mediator.Send(new GetSupportTicketDetailQuery(id), cancellationToken);
        return Ok(ApiResponse<SupportTicketDetailDto>.Ok(ticket));
    }

    /// <summary>
    /// POST /admin/support-tickets/{id}/reply â€” reply to a support ticket.
    /// </summary>
    [HttpPost("support-tickets/{id:guid}/reply")]
    public async Task<IActionResult> ReplyToSupportTicket(
        Guid id, [FromBody] string message, CancellationToken cancellationToken)
    {
        var replyDto = await _mediator.Send(new ReplyToSupportTicketCommand(id, AdminId, message), cancellationToken);
        return Ok(ApiResponse<TicketMessageDto>.Ok(replyDto));
    }

    /// <summary>
    /// PATCH /admin/support-tickets/{id}/close â€” resolve and close a support ticket.
    /// </summary>
    [HttpPatch("support-tickets/{id:guid}/close")]
    public async Task<IActionResult> CloseSupportTicket(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new CloseSupportTicketCommand(id), cancellationToken);
        return NoContent();
    }


    // ── Disputes (product reports) ────────────────────────────────────────

    /// <summary>GET /admin/disputes — list product reports (dispute_handling_resolution screen).</summary>
    [HttpGet("disputes")]
    public async Task<IActionResult> GetDisputes(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool? isResolved = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetDisputesQuery(pageNumber, pageSize, isResolved), cancellationToken);
        return Ok(ApiResponse<System.Collections.Generic.IReadOnlyList<DisputeDto>>.Ok(result));
    }

    /// <summary>GET /admin/disputes/{id} — get a single dispute detail by ID.</summary>
    [HttpGet("disputes/{id:guid}")]
    public async Task<IActionResult> GetDisputeById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetDisputeByIdQuery(id), cancellationToken);
        return Ok(ApiResponse<DisputeDto>.Ok(result));
    }

    /// <summary>PATCH /admin/disputes/{id}/resolve — mark a product report as resolved.</summary>
    [HttpPatch("disputes/{id:guid}/resolve")]
    public async Task<IActionResult> ResolveDispute(Guid id, [FromBody] ResolveDisputeRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ResolveDisputeCommand(id, AdminId, request.AdminNote), cancellationToken);
        return Ok(ApiResponse<DisputeDto>.Ok(result));
    }

    // ── System Settings ───────────────────────────────────────────────────

    /// <summary>
    /// GET /admin/system-settings — returns the current platform-wide operational configuration.
    /// </summary>
    [HttpGet("system-settings")]
    public async Task<IActionResult> GetSystemSettings(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSystemSettingsQuery(), cancellationToken);
        return Ok(ApiResponse<SystemSettingsDto>.Ok(result));
    }

    /// <summary>
    /// POST /admin/system-settings — persist updated platform-wide operational configuration.
    /// Saves all fields shown on the Platform Admin → System Settings screen.
    /// </summary>
    [HttpPost("system-settings")]
    public async Task<IActionResult> SaveSystemSettings(
        [FromBody] SaveSystemSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SaveSystemSettingsCommand(
            AdminId,
            request.MaxDiscountPerCyclePercent,
            request.DefaultPriceFloorPolicy,
            request.NewBusinessDefaultAutomationMode,
            request.AutoVerifyPartnerStores,
            request.BulkProductUploadEnabled,
            request.PlatformCommissionPercent,
            request.ApiRequestRateLimitPerMinute);

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<SystemSettingsDto>.Ok(result));
    }
}

public class ProductModerationRequest { public string? Note { get; set; } }
public class ResolveDisputeRequest { [System.ComponentModel.DataAnnotations.Required] public string AdminNote { get; set; } = null!; }

public class SaveSystemSettingsRequest
{
    /// <summary>Hard ceiling on AI auto-discount per cycle. Must be 1–15.</summary>
    [System.ComponentModel.DataAnnotations.Range(1, 15)]
    public int MaxDiscountPerCyclePercent { get; set; } = 10;

    /// <summary>DynamicAi | Fixed30Percent | Fixed50Percent</summary>
    [System.ComponentModel.DataAnnotations.Required]
    public string DefaultPriceFloorPolicy { get; set; } = "DynamicAi";

    /// <summary>Manual | Assisted | Autonomous</summary>
    [System.ComponentModel.DataAnnotations.Required]
    public string NewBusinessDefaultAutomationMode { get; set; } = "Assisted";

    public bool AutoVerifyPartnerStores { get; set; }
    public bool BulkProductUploadEnabled { get; set; } = true;

    [System.ComponentModel.DataAnnotations.Range(0, 100)]
    public int PlatformCommissionPercent { get; set; } = 10;

    [System.ComponentModel.DataAnnotations.Range(1, 10000)]
    public int ApiRequestRateLimitPerMinute { get; set; } = 120;
}