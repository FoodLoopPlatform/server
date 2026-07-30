using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Application.Features.Admin.Queries;
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
    /// GET /admin/stores/pending — lists all stores awaiting verification review.
    /// Accessible without auth so the admin frontend can display the queue.
    /// Each entry includes the owner's contact details and all uploaded documents.
    /// </summary>
    [HttpGet("stores/pending")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPendingStores(CancellationToken cancellationToken)
    {
        var stores = await _mediator.Send(new GetPendingStoresQuery(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminStoreDto>>.Ok(stores));
    }

    /// <summary>
    /// GET /admin/stores/{id} — full store detail with all documents for a single review.
    /// Accessible without auth so the admin frontend can deep-link to a specific review.
    /// </summary>
    [HttpGet("stores/{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStoreForReview(Guid id, CancellationToken cancellationToken)
    {
        var store = await _mediator.Send(new GetStoreForReviewQuery(id), cancellationToken);
        return Ok(ApiResponse<AdminStoreDto>.Ok(store));
    }

    /// <summary>
    /// PATCH /admin/stores/{id}/verify — approve or reject a store.
    /// Action must be "Approved" or "Rejected".
    /// On approval the owner's account is activated; on rejection it stays PendingVerification
    /// so they can correct and re-submit.
    /// </summary>
    [HttpPatch("stores/{id:guid}/verify")]
    public async Task<IActionResult> VerifyStore(Guid id, [FromBody] VerifyStoreRequest request, CancellationToken cancellationToken)
    {
        var store = await _mediator.Send(new VerifyStoreCommand(id, AdminId, request), cancellationToken);
        return Ok(ApiResponse<AdminStoreDto>.Ok(store));
    }

    /// <summary>
    /// PATCH /admin/charities/{id}/verify — approve or reject a charity's onboarding verification.
    /// Action must be "Approved" or "Rejected".
    /// </summary>
    [HttpPatch("charities/{id:guid}/verify")]
    public async Task<IActionResult> VerifyCharity(Guid id, [FromBody] VerifyStoreRequest request, CancellationToken cancellationToken)
    {
        var store = await _mediator.Send(new VerifyStoreCommand(id, AdminId, request), cancellationToken);
        return Ok(ApiResponse<AdminStoreDto>.Ok(store));
    }

    // ── User management ──────────────────────────────────────────────────────

    /// <summary>
    /// PATCH /admin/users/{id}/status — suspend, ban, or reactivate a user.
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
    /// GET /admin/users/{id}/activity-log — recent events for a user (account created,
    /// documents verified, orders placed, support tickets).
    /// </summary>
    [HttpGet("users/{id:guid}/activity-log")]
    public async Task<IActionResult> GetUserActivityLog(Guid id, CancellationToken cancellationToken)
    {
        var log = await _mediator.Send(new GetUserActivityLogQuery(id), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ActivityLogEntryDto>>.Ok(log));
    }

    // ── Analytics ─────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /admin/analytics/summary — high-level metrics for dashboard (total users, stores, sales, savings).
    /// </summary>
    [HttpGet("analytics/summary")]
    public async Task<IActionResult> GetAnalyticsSummary(CancellationToken cancellationToken)
    {
        var summary = await _mediator.Send(new GetAnalyticsSummaryQuery(), cancellationToken);
        return Ok(ApiResponse<AnalyticsSummaryDto>.Ok(summary));
    }

    // ── Store moderation ──────────────────────────────────────────────────────

    /// <summary>
    /// GET /admin/stores — list all stores with optional VerificationStatus filter.
    /// </summary>
    [HttpGet("stores")]
    public async Task<IActionResult> GetStores(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] VerificationStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var stores = await _mediator.Send(new GetAdminStoresQuery(pageNumber, pageSize, status), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminStoreDto>>.Ok(stores));
    }

    /// <summary>
    /// GET /admin/charities — list all charities with optional VerificationStatus filter.
    /// </summary>
    [HttpGet("charities")]
    public async Task<IActionResult> GetCharities(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] VerificationStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var charities = await _mediator.Send(new GetAdminCharitiesQuery(pageNumber, pageSize, status), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminStoreDto>>.Ok(charities));
    }

    // ── Review moderation ─────────────────────────────────────────────────────

    /// <summary>
    /// GET /admin/reviews — list all reviews with optional Rating and StoreId filters.
    /// </summary>
    [HttpGet("reviews")]
    public async Task<IActionResult> GetReviews(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? rating = null,
        [FromQuery] Guid? storeId = null,
        CancellationToken cancellationToken = default)
    {
        var reviews = await _mediator.Send(new GetAdminReviewsQuery(pageNumber, pageSize, rating, storeId), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminReviewDto>>.Ok(reviews));
    }

    /// <summary>
    /// DELETE /admin/reviews/{id} — moderate and remove an inappropriate review.
    /// </summary>
    [HttpDelete("reviews/{id:guid}")]
    public async Task<IActionResult> DeleteReview(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteReviewCommand(id), cancellationToken);
        return NoContent();
    }

    // ── Product moderation ───────────────────────────────────────────

    /// <summary>
    /// GET /admin/products — list all products with optional Status and StoreId filters.
    /// </summary>
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] Guid? storeId = null,
        CancellationToken cancellationToken = default)
    {
        var products = await _mediator.Send(new GetAdminProductsQuery(pageNumber, pageSize, status, storeId), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminProductDto>>.Ok(products));
    }

    /// <summary>
    /// DELETE /admin/products/{id} — suspend and soft-delete a product.
    /// </summary>
    [HttpDelete("products/{id:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new AdminDeleteProductCommand(id), cancellationToken);
        return NoContent();
    }

    // ── Support Tickets ───────────────────────────────────────────────────────

    /// <summary>
    /// GET /admin/support-tickets — list support tickets with status and priority filters.
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
    /// GET /admin/support-tickets/{id} — get a ticket detail with full conversation history.
    /// </summary>
    [HttpGet("support-tickets/{id:guid}")]
    public async Task<IActionResult> GetSupportTicketDetail(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await _mediator.Send(new GetSupportTicketDetailQuery(id), cancellationToken);
        return Ok(ApiResponse<SupportTicketDetailDto>.Ok(ticket));
    }

    /// <summary>
    /// POST /admin/support-tickets/{id}/reply — reply to a support ticket.
    /// </summary>
    [HttpPost("support-tickets/{id:guid}/reply")]
    public async Task<IActionResult> ReplyToSupportTicket(
        Guid id, [FromBody] string message, CancellationToken cancellationToken)
    {
        var replyDto = await _mediator.Send(new ReplyToSupportTicketCommand(id, AdminId, message), cancellationToken);
        return Ok(ApiResponse<TicketMessageDto>.Ok(replyDto));
    }

    /// <summary>
    /// PATCH /admin/support-tickets/{id}/close — resolve and close a support ticket.
    /// </summary>
    [HttpPatch("support-tickets/{id:guid}/close")]
    public async Task<IActionResult> CloseSupportTicket(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new CloseSupportTicketCommand(id), cancellationToken);
        return NoContent();
    }
}
