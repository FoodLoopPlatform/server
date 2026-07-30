using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Listings;
using FoodLoop.Application.Features.Listings.Commands;
using FoodLoop.Application.Features.Listings.Queries;
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
[Route("stores/me/listings")]
[Authorize(Roles = AppRole.Merchant)]
public class MerchantListingsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalizationService _loc;

    public MerchantListingsController(IMediator mediator, ICurrentUserService currentUser, ILocalizationService loc)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _loc = loc;
    }

    private Guid OwnerId => _currentUser.UserId ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// POST /stores/me/listings — create a new product listing.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateListing([FromBody] CreateProductListingRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateProductListingCommand(
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

        var listing = await _mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<ProductListingDto>.Ok(listing));
    }

    /// <summary>
    /// GET /stores/me/listings — list all product listings belonging to the caller's store.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyListings(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetMyListingsQuery(OwnerId, pageNumber, pageSize, categoryId, status, searchTerm);
        var listings = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ProductListingDto>>.Ok(listings));
    }

    /// <summary>
    /// GET /stores/me/listings/{id} — get details of a single product listing.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetListingDetail(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetListingDetailQuery(OwnerId, id);
        var listing = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<ProductListingDto>.Ok(listing));
    }

    /// <summary>
    /// PATCH /stores/me/listings/{id} — update a product listing.
    /// </summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateListing(Guid id, [FromBody] UpdateProductListingRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateProductListingCommand(
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
            request.Status);

        var listing = await _mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<ProductListingDto>.Ok(listing));
    }

    /// <summary>
    /// DELETE /stores/me/listings/{id} — soft-delete a product listing.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteListing(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeleteProductListingCommand(OwnerId, id);
        await _mediator.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(_loc["ListingDeletedSuccessfully"]));
    }

    /// <summary>
    /// POST /stores/me/listings/{id}/images — upload an image for a product listing.
    /// </summary>
    [HttpPost("{id:guid}/images")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file, CancellationToken cancellationToken)
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

        var command = new UploadListingImageCommand(OwnerId, id, uploadRequest);
        var listing = await _mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<ProductListingDto>.Ok(listing));
    }

    /// <summary>
    /// POST /stores/me/listings/bulk — upload product listings in bulk via CSV.
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

        var command = new BulkUploadListingsCommand(OwnerId, uploadRequest);
        var listings = await _mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ProductListingDto>>.Ok(listings));
    }
}

public class CreateProductListingRequest
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

public class UpdateProductListingRequest
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
    public string? Status { get; set; }
}
