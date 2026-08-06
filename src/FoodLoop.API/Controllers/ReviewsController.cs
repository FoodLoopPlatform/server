using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Reviews;
using FoodLoop.Application.Features.Reviews.Commands;
using FoodLoop.Application.Features.Reviews.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.API.Controllers;

[ApiController]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ReviewsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    private Guid UserId => _currentUser.UserId ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// POST /reviews — submits a review for an order.
    /// </summary>
    [HttpPost("reviews")]
    public async Task<IActionResult> SubmitReview([FromBody] SubmitReviewRequest request, CancellationToken cancellationToken)
    {
        var command = new SubmitReviewCommand(UserId, request.OrderId, request.Rating, request.Comment);
        var result = await _mediator.Send(command, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(ApiResponse.Fail(result.Message ?? "Failed to submit review"));
        }
        return Ok(ApiResponse<ReviewDto>.Ok(result.Data!));
    }

    /// <summary>
    /// GET /stores/{id}/reviews — gets reviews for a store.
    /// </summary>
    [HttpGet("stores/{id:guid}/reviews")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStoreReviews(
        Guid id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetOrganizationReviewsQuery(id, pageNumber, pageSize);
        var reviews = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ReviewDto>>.Ok(reviews));
    }
}

public class SubmitReviewRequest
{
    public Guid OrderId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
}
