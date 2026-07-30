using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Listings;
using FoodLoop.Application.Features.Listings.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Features.Stores;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Listings;

public class UploadListingImageCommandHandler : IRequestHandler<UploadListingImageCommand, ProductListingDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorage;

    public UploadListingImageCommandHandler(IUnitOfWork unitOfWork, IFileStorageService fileStorage)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
    }

    public async Task<ProductListingDto> Handle(UploadListingImageCommand command, CancellationToken cancellationToken)
    {
        var store = await _unitOfWork.FindByOwnerOrThrowAsync(command.OwnerId, "Store not found.", cancellationToken);

        var listing = await _unitOfWork.Repository<ProductListing>().Query()
            .Include(l => l.Category)
            .Include(l => l.Images)
            .FirstOrDefaultAsync(l => l.Id == command.ListingId && l.StoreId == store.Id && !l.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ProductListing", command.ListingId);

        var imageUrl = await _fileStorage.SaveAsync(command.File, $"listings/{listing.Id}", cancellationToken);

        var displayOrder = listing.Images.Any() ? listing.Images.Max(i => i.DisplayOrder) + 1 : 0;

        var productImage = new ProductImage
        {
            ListingId = listing.Id,
            ImageUrl = imageUrl,
            DisplayOrder = displayOrder
        };

        _unitOfWork.Repository<ProductImage>().Add(productImage);
        listing.Images.Add(productImage);
        listing.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return listing.ToDto();
    }
}
