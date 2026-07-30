using FoodLoop.Application.DTOs.Listings;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Listings.Commands;

public record UpdateProductListingCommand(
    Guid OwnerId,
    Guid ListingId,
    Guid? CategoryId,
    string? Title,
    string? TitleAr,
    string? Description,
    string? DescriptionAr,
    decimal? OriginalPrice,
    decimal? DiscountedPrice,
    int? QuantityAvailable,
    DateOnly? ExpirationDate,
    string? Status) : IRequest<ProductListingDto>;
