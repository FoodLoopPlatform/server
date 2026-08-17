using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FoodLoop.API.Common;
using FoodLoop.Application.Common.DTOs.AI;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Features.AiIntegration.Commands;
using FoodLoop.Application.Features.AiIntegration.Queries;
using FoodLoop.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.API.Controllers;

[ApiController]
[Route("stores/me/ai-recommendations")]
[Authorize(Roles = AppRole.Merchant)]
public class AiRecommendationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public AiRecommendationsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    private Guid OwnerId => _currentUser.UserId ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// GET stores/me/ai-recommendations — list Pending AiPricingRecommendation rows for the current merchant's own store.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPendingRecommendations(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPendingAiRecommendationsQuery(OwnerId), cancellationToken);
        if (!result.Success)
        {
            return BadRequest(ApiResponse<object?>.Fail(result.Message ?? "Failed to fetch recommendations."));
        }
        return Ok(ApiResponse<IReadOnlyList<AiPricingRecommendationDto>>.Ok(result.Data!));
    }

    /// <summary>
    /// POST stores/me/ai-recommendations/{id}/approve — approve a recommendation.
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApproveRecommendation(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ApproveAiRecommendationCommand(OwnerId, id), cancellationToken);
        if (!result.Success)
        {
            return BadRequest(ApiResponse<object?>.Fail(result.Message ?? "Failed to approve recommendation."));
        }
        return Ok(ApiResponse.Ok("Recommendation approved successfully."));
    }

    /// <summary>
    /// POST stores/me/ai-recommendations/{id}/reject — reject a recommendation.
    /// </summary>
    [HttpPost("{id}/reject")]
    public async Task<IActionResult> RejectRecommendation(Guid id, [FromBody] RejectRecommendationRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RejectAiRecommendationCommand(OwnerId, id, request.Reason), cancellationToken);
        if (!result.Success)
        {
            return BadRequest(ApiResponse<object?>.Fail(result.Message ?? "Failed to reject recommendation."));
        }
        return Ok(ApiResponse.Ok("Recommendation rejected successfully."));
    }
}

public class RejectRecommendationRequest
{
    public string? Reason { get; set; }
}
