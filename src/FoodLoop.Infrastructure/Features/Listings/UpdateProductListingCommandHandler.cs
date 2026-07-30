using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Listings;
using FoodLoop.Application.Features.Listings.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Stores;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Listings;

public class UpdateProductListingCommandHandler : IRequestHandler<UpdateProductListingCommand, ProductListingDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProductListingCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ProductListingDto> Handle(UpdateProductListingCommand command, CancellationToken cancellationToken)
    {
        var store = await _unitOfWork.FindByOwnerOrThrowAsync(command.OwnerId, "Store not found.", cancellationToken);

        var listing = await _unitOfWork.Repository<ProductListing>().Query()
            .Include(l => l.Category)
            .Include(l => l.Images)
            .FirstOrDefaultAsync(l => l.Id == command.ListingId && l.StoreId == store.Id && !l.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ProductListing", command.ListingId);

        if (command.CategoryId.HasValue)
        {
            var category = await _unitOfWork.Repository<Category>().GetByIdAsync(command.CategoryId.Value, cancellationToken)
                ?? throw new NotFoundException("Category", command.CategoryId.Value);
            listing.CategoryId = command.CategoryId.Value;
            listing.Category = category;
        }

        if (command.Title != null) listing.Title = command.Title;
        if (command.TitleAr != null) listing.TitleAr = command.TitleAr;
        if (command.Description != null) listing.Description = command.Description;
        if (command.DescriptionAr != null) listing.DescriptionAr = command.DescriptionAr;

        var origPrice = command.OriginalPrice ?? listing.OriginalPrice;
        var discPrice = command.DiscountedPrice ?? listing.DiscountedPrice;

        if (origPrice < 0 || discPrice < 0)
        {
            throw new ArgumentException("Prices cannot be negative.");
        }

        if (discPrice > origPrice)
        {
            throw new ArgumentException("Discounted price cannot be greater than original price.");
        }

        listing.OriginalPrice = origPrice;
        listing.DiscountedPrice = discPrice;

        if (command.QuantityAvailable.HasValue)
        {
            if (command.QuantityAvailable.Value < 0)
            {
                throw new ArgumentException("Quantity available cannot be negative.");
            }
            listing.QuantityAvailable = command.QuantityAvailable.Value;
        }

        if (command.ExpirationDate.HasValue)
        {
            listing.ExpirationDate = command.ExpirationDate.Value;
        }

        if (!string.IsNullOrWhiteSpace(command.Status))
        {
            if (Enum.TryParse<ListingStatus>(command.Status, true, out var status))
            {
                listing.Status = status;
            }
            else
            {
                throw new ArgumentException($"Invalid ListingStatus value: {command.Status}");
            }
        }

        listing.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return listing.ToDto();
    }
}
