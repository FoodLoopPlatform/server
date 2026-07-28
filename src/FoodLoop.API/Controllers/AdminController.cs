using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Admin;
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
}
