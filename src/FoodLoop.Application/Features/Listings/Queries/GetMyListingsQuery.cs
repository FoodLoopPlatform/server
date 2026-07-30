using FoodLoop.Application.DTOs.Listings;
using MediatR;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Listings.Queries;

public record GetMyListingsQuery(
    Guid OwnerId,
    int PageNumber = 1,
    int PageSize = 10,
    Guid? CategoryId = null,
    string? Status = null,
    string? SearchTerm = null) : IRequest<IReadOnlyList<ProductListingDto>>;
