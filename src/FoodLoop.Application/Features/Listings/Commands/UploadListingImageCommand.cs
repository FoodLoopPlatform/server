using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Listings;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Listings.Commands;

public record UploadListingImageCommand(
    Guid OwnerId,
    Guid ListingId,
    FileUploadRequest File) : IRequest<ProductListingDto>;
