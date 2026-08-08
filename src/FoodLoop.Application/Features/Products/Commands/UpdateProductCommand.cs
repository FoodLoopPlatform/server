using FoodLoop.Application.DTOs.Products;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Products.Commands;

public record UpdateProductCommand(
    Guid OwnerId,
    Guid ProductId,
    Guid? CategoryId,
    string? Title,
    string? Description,
    decimal? OriginalPrice,
    decimal? DiscountedPrice,
    int? QuantityAvailable,
    DateOnly? ExpirationDate,
    string? Status) : IRequest<ProductDto>;


