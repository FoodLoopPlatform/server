using FoodLoop.Application.DTOs.Listings;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Listings.Commands;

public record CreateProductListingCommand(
    Guid OwnerId,
    Guid CategoryId,
    string Title,
    string? TitleAr,
    string? Description,
    string? DescriptionAr,
    decimal OriginalPrice,
    decimal DiscountedPrice,
    int QuantityAvailable,
    DateOnly ExpirationDate) : IRequest<ProductListingDto>;
