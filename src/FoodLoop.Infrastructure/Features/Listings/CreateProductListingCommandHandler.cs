using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Listings;
using FoodLoop.Application.Features.Listings.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Stores;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Listings;

public class CreateProductListingCommandHandler : IRequestHandler<CreateProductListingCommand, ProductListingDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateProductListingCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ProductListingDto> Handle(CreateProductListingCommand command, CancellationToken cancellationToken)
    {
        var store = await _unitOfWork.FindByOwnerOrThrowAsync(command.OwnerId, "Store not found.", cancellationToken);
        if (store.VerificationStatus != VerificationStatus.Verified)
        {
            throw new ArgumentException("Your store must be verified by an admin before you can manage product listings.");
        }

        var category = await _unitOfWork.Repository<Category>().GetByIdAsync(command.CategoryId, cancellationToken)
            ?? throw new NotFoundException("Category", command.CategoryId);

        if (command.OriginalPrice < 0 || command.DiscountedPrice < 0)
        {
            throw new ArgumentException("Prices cannot be negative.");
        }

        if (command.DiscountedPrice > command.OriginalPrice)
        {
            throw new ArgumentException("Discounted price cannot be greater than original price.");
        }

        if (command.QuantityAvailable < 0)
        {
            throw new ArgumentException("Quantity available cannot be negative.");
        }

        var listing = new ProductListing
        {
            StoreId = store.Id,
            CategoryId = command.CategoryId,
            Title = command.Title,
            TitleAr = command.TitleAr,
            Description = command.Description,
            DescriptionAr = command.DescriptionAr,
            OriginalPrice = command.OriginalPrice,
            DiscountedPrice = command.DiscountedPrice,
            QuantityAvailable = command.QuantityAvailable,
            ExpirationDate = command.ExpirationDate,
            Status = ListingStatus.Active
        };

        _unitOfWork.Repository<ProductListing>().Add(listing);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Fetch category info to populate DTO
        listing.Category = category;

        return listing.ToDto();
    }
}
