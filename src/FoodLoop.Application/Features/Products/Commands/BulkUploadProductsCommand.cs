using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Listings;
using MediatR;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Products.Commands;

public record BulkUploadProductsCommand(
    Guid OwnerId,
    FileUploadRequest File) : IRequest<IReadOnlyList<ProductDto>>;

