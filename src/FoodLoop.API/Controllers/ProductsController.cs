using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Application.Features.Products.Queries;
using FoodLoop.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.API.Controllers;

[ApiController]
[Route("stores/me/products")]
[Authorize(Roles = AppRole.Merchant)]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalizationService _loc;

    public ProductsController(IMediator mediator, ICurrentUserService currentUser, ILocalizationService loc)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _loc = loc;
    }

    private Guid OwnerId => _currentUser.UserId ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// POST /organizations/me/products Ã¢â‚¬â€ create a new product.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateProductCommand(
            OwnerId,
            request.CategoryId,
            request.Title,
            request.TitleAr,
            request.Description,
            request.DescriptionAr,
            request.OriginalPrice,
            request.DiscountedPrice,
            request.QuantityAvailable,
            request.ExpirationDate);

        var product = await _mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<ProductDto>.Ok(product));
    }

    /// <summary>
    /// GET /organizations/me/products Ã¢â‚¬â€ list all products belonging to the caller's organization.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyProducts(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetMyProductsQuery(OwnerId, pageNumber, pageSize, categoryId, status, searchTerm);
        var products = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ProductDto>>.Ok(products));
    }

    /// <summary>
    /// GET /organizations/me/products/{id} Ã¢â‚¬â€ get details of a single product.
    /// </summary>
    /// <summary>
    /// GET /stores/me/products/pricing — store-level pricing and discount overview metrics.
    /// Declared before {id:guid} so the literal segment "pricing" is matched first.
    /// </summary>
    [HttpGet("pricing")]
    public async Task<IActionResult> GetPricingOverview(CancellationToken cancellationToken)
    {
        var query = new GetStorePricingOverviewQuery(OwnerId);
        var overview = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<StorePricingOverviewDto>.Ok(overview));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProductDetail(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetProductDetailQuery(OwnerId, id);
        var product = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<ProductDto>.Ok(product));
    }

    /// <summary>
    /// PATCH /organizations/me/products/{id} Ã¢â‚¬â€ update a product (Form Data).
    /// </summary>
    [HttpPatch("{id:guid}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromForm] UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateProductCommand(
            OwnerId,
            id,
            request.CategoryId,
            request.Title,
            request.TitleAr,
            request.Description,
            request.DescriptionAr,
            request.OriginalPrice,
            request.DiscountedPrice,
            request.QuantityAvailable,
            request.ExpirationDate,
            request.Status?.ToString());

        var product = await _mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<ProductDto>.Ok(product));
    }

    /// <summary>
    /// DELETE /organizations/me/products/{id} Ã¢â‚¬â€ soft-delete a product.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeleteProductCommand(OwnerId, id);
        await _mediator.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(_loc["ListingDeletedSuccessfully"]));
    }

    /// <summary>
    /// POST /organizations/me/products/{id}/images Ã¢â‚¬â€ upload an image for a product.
    /// </summary>
    [HttpPost("{id:guid}/images")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse.Fail(_loc["FileRequired"]));
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        if (!allowedExtensions.Contains(ext))
        {
            return BadRequest(ApiResponse.Fail(_loc["InvalidImageFormat"]));
        }

        await using var stream = file.OpenReadStream();
        var uploadRequest = new FileUploadRequest
        {
            Content = stream,
            FileName = file.FileName,
            ContentType = file.ContentType
        };

        var command = new UploadProductImageCommand(OwnerId, id, uploadRequest);
        var product = await _mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<ProductDto>.Ok(product));
    }

    /// <summary>
    /// DELETE /organizations/me/products/{id}/images/{imageId} Ã¢â‚¬â€ delete an image of a product.
    /// </summary>
    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        var command = new DeleteProductImageCommand(OwnerId, id, imageId);
        var product = await _mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<ProductDto>.Ok(product));
    }

    /// <summary>
    /// POST /organizations/me/products/bulk Ã¢â‚¬â€ upload products in bulk via CSV.
    /// </summary>
    [HttpPost("bulk")]
    [RequestSizeLimit(15_000_000)]
    public async Task<IActionResult> BulkUpload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse.Fail(_loc["FileRequired"]));
        }

        await using var stream = file.OpenReadStream();
        var uploadRequest = new FileUploadRequest
        {
            Content = stream,
            FileName = file.FileName,
            ContentType = file.ContentType
        };

        var command = new BulkUploadProductsCommand(OwnerId, uploadRequest);
        var products = await _mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ProductDto>>.Ok(products));
    }
}

public class CreateProductRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    [Required, MinLength(2), MaxLength(200)]
    public string Title { get; set; } = null!;

    public string? TitleAr { get; set; }
    public string? Description { get; set; }
    public string? DescriptionAr { get; set; }

    [Required, Range(0.0, 1000000.0)]
    public decimal OriginalPrice { get; set; }

    [Required, Range(0.0, 1000000.0)]
    public decimal DiscountedPrice { get; set; }

    [Required, Range(0, 100000)]
    public int QuantityAvailable { get; set; }

    [Required]
    public DateOnly ExpirationDate { get; set; }
}

public class UpdateProductRequest
{
    public Guid? CategoryId { get; set; }
    public string? Title { get; set; }
    public string? TitleAr { get; set; }
    public string? Description { get; set; }
    public string? DescriptionAr { get; set; }
    public decimal? OriginalPrice { get; set; }
    public decimal? DiscountedPrice { get; set; }
    public int? QuantityAvailable { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public ProductStatus? Status { get; set; }
}



