using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Listings;
using MediatR;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Listings.Commands;

public record BulkUploadListingsCommand(
    Guid OwnerId,
    FileUploadRequest File) : IRequest<IReadOnlyList<ProductListingDto>>;
