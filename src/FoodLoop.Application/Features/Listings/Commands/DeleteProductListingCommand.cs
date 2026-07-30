using MediatR;
using System;

namespace FoodLoop.Application.Features.Listings.Commands;

public record DeleteProductListingCommand(Guid OwnerId, Guid ListingId) : IRequest;
