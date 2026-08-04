using FoodLoop.Application.DTOs.Listings;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Listings.Commands;

public record DeleteProductImageCommand(
    Guid OwnerId,
    Guid ProductId,
    Guid ImageId) : IRequest<ProductDto>;
