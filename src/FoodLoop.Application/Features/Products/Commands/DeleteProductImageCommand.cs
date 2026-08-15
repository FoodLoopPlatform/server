using FoodLoop.Application.DTOs.Products;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Products.Commands;

public record DeleteProductImageCommand(
    Guid OwnerId,
    Guid ProductId,
    Guid ImageId) : IRequest<ProductDto>;


