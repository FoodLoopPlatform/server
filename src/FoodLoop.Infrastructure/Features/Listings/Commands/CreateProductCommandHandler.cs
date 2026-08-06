using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Listings;
using FoodLoop.Application.Features.Listings.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Organizations;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Listings.Commands;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, ProductDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateProductCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ProductDto> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var organization = await _unitOfWork.FindByOwnerOrThrowAsync(command.OwnerId, "Organization not found.", cancellationToken);
        if (organization.VerificationStatus != VerificationStatus.Verified)
        {
            throw new ArgumentException("Your organization must be verified by an admin before you can manage products.");
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

        var product = new Product
        {
            OrganizationId = organization.Id,
            CategoryId = command.CategoryId,
            Title = command.Title,
            TitleAr = command.TitleAr,
            Description = command.Description,
            DescriptionAr = command.DescriptionAr,
            OriginalPrice = command.OriginalPrice,
            DiscountedPrice = command.DiscountedPrice,
            QuantityAvailable = command.QuantityAvailable,
            ExpirationDate = command.ExpirationDate,
            Status = ProductStatus.Active
        };

        _unitOfWork.Repository<Product>().Add(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Fetch category info to populate DTO
        product.Category = category;

        return product.ToDto();
    }
}


