using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Application.Features.Products.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.API.Controllers;

[ApiController]
[Route("marketplace")]
[AllowAnonymous]
public class MarketplaceController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public MarketplaceController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// GET /marketplace/products — searches and filters products based on user location, category, search query, and price range.
    /// </summary>
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] double? latitude = null,
        [FromQuery] double? longitude = null,
        [FromQuery] double? maxDistance = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetMarketplaceProductsQuery(
            latitude,
            longitude,
            maxDistance,
            categoryId,
            minPrice,
            maxPrice,
            search,
            sortBy,
            pageNumber,
            pageSize);

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<MarketplaceProductDto>>.Ok(result));
    }

    /// <summary>
    /// GET /marketplace/products/{id} — public product detail page (product_details screen).
    /// </summary>
    [HttpGet("products/{id:guid}")]
    public async Task<IActionResult> GetProductDetail(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMarketplaceProductDetailQuery(id), cancellationToken);
        return Ok(ApiResponse<MarketplaceProductDto>.Ok(result));
    }

    /// <summary>
    /// POST /marketplace/products/{id}/report — report a listing (report_an_issue screen).
    /// Requires authentication so we can track who filed the report.
    /// </summary>
    [HttpPost("products/{id:guid}/report")]
    [Authorize]
    public async Task<IActionResult> ReportProduct(Guid id, [FromBody] ReportProductRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        await _mediator.Send(new ReportProductCommand(userId, id, request.Reason, request.Details, request.ImageUrl), cancellationToken);
        return Ok(ApiResponse.Ok("Report submitted. Our team will review it shortly."));
    }
}

public class ReportProductRequest
{
    /// <summary>MisleadingInfo | WrongExpiry | Spam | Inappropriate | Other</summary>
    [Required]
    public string Reason { get; set; } = null!;
    public string? Details { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }
}
