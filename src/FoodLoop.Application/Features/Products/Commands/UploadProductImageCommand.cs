using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Products;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Products.Commands;

public record UploadProductImageCommand(
    Guid OwnerId,
    Guid ProductId,
    FileUploadRequest File) : IRequest<ProductDto>;


