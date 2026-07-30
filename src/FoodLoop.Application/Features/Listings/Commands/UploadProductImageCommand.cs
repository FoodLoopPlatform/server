using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Listings;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Listings.Commands;

public record UploadProductImageCommand(
    Guid OwnerId,
    Guid ProductId,
    FileUploadRequest File) : IRequest<ProductDto>;
