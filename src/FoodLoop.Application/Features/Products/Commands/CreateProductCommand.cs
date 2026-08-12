using FoodLoop.Application.DTOs.Products;
using FoodLoop.Domain.Enums;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Products.Commands;

public record CreateProductCommand(
    Guid OwnerId,
    Guid CategoryId,
    string Title,
    string? Description,
    decimal OriginalPrice,
    decimal DiscountedPrice,
    int QuantityAvailable,
    DateOnly ExpirationDate,
    ExpiryVerificationState? ExpiryVerificationState = null,
    double? OcrConfidence = null,
    string? OcrText = null) : IRequest<ProductDto>;


