using FoodLoop.Application.DTOs.Listings;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Products.Commands;

public record DeleteProductImageCommand(
    Guid OwnerId,
    Guid ProductId,
    Guid ImageId) : IRequest<ProductDto>;

