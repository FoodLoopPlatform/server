using MediatR;
using FoodLoop.Application.DTOs.Products;
using System;

namespace FoodLoop.Application.Features.Products.Commands;

/// <summary>PATCH /stores/me/products/{id}/discount — apply or update a discount on a product.</summary>
public record ApplyDiscountCommand(
    Guid OwnerId,
    Guid ProductId,
    decimal DiscountedPrice,
    string? ChangeReason) : IRequest<ProductDto>;
