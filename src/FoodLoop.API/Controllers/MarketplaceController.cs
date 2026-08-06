using FoodLoop.API.Common;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Products.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.API.Controllers;

[ApiController]
[Route("marketplace")]
[AllowAnonymous]
public class MarketplaceController : ControllerBase
{
    private readonly IMediator _mediator;

    public MarketplaceController(IMediator mediator)
    {
        _mediator = mediator;
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
}
