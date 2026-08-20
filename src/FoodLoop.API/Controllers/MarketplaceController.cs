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
    private readonly ILocalizationService _loc;

    public MarketplaceController(IMediator mediator, ICurrentUserService currentUser, ILocalizationService loc)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _loc = loc;
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
    /// Accepts multipart/form-data with an optional evidence image file.
    /// Requires authentication so we can track who filed the report.
    /// </summary>
    [HttpPost("products/{id:guid}/report")]
    [Consumes("multipart/form-data")]
    [Authorize]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> ReportProduct(Guid id, [FromForm] ReportProductRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        FoodLoop.Application.Common.Models.FileUploadRequest? fileUpload = null;
        if (request.Image != null && request.Image.Length > 0)
        {
            var ext = System.IO.Path.GetExtension(request.Image.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            if (!System.Linq.Enumerable.Contains(allowedExtensions, ext))
            {
                return BadRequest(ApiResponse.Fail(_loc["InvalidImageFormat"]));
            }

            var stream = request.Image.OpenReadStream();
            fileUpload = new FoodLoop.Application.Common.Models.FileUploadRequest
            {
                Content = stream,
                FileName = request.Image.FileName,
                ContentType = request.Image.ContentType
            };
        }

        await _mediator.Send(new ReportProductCommand(userId, id, request.Reason, request.Details, fileUpload), cancellationToken);
        return Ok(ApiResponse.Ok("Report submitted. Our team will review it shortly."));
    }
}

public class ReportProductRequest
{
    /// <summary>Issue category: MisleadingInfo | WrongExpiry | Expired | Spam | Inappropriate | Other</summary>
    [Required]
    public FoodLoop.Domain.Enums.ProductReportReason Reason { get; set; }

    /// <summary>Optional descriptive details about the issue.</summary>
    public string? Details { get; set; }

    /// <summary>Optional evidence image file uploaded directly from device camera or gallery.</summary>
    public Microsoft.AspNetCore.Http.IFormFile? Image { get; set; }
}
