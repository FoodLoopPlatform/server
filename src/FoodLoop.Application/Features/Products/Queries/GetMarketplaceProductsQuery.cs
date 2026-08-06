using FoodLoop.Application.DTOs.Products;
using MediatR;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Products.Queries;

public record GetMarketplaceProductsQuery(
    double? UserLatitude,
    double? UserLongitude,
    double? MaxDistanceKm,
    Guid? CategoryId,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? SearchTerm,
    string? SortBy, // "distance", "price_asc", "price_desc", "discount", "expiration"
    int PageNumber,
    int PageSize
) : IRequest<IReadOnlyList<MarketplaceProductDto>>;
