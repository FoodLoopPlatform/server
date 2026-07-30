using FoodLoop.Application.DTOs.Listings;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Listings.Queries;

public record GetListingDetailQuery(Guid OwnerId, Guid ListingId) : IRequest<ProductListingDto>;
