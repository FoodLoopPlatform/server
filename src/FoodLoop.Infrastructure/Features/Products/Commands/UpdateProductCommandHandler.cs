using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Organizations;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Products.Commands;

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, ProductDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;

    public UpdateProductCommandHandler(IUnitOfWork unitOfWork, IAuditLogService auditLogService)
    {
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
    }

    public async Task<ProductDto> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var organization = await _unitOfWork.FindByOwnerOrThrowAsync(command.OwnerId, "Organization not found.", cancellationToken);

        var product = await _unitOfWork.Repository<Product>().Query()
            .Include(l => l.Category)
            .Include(l => l.Images)
            .FirstOrDefaultAsync(l => l.Id == command.ProductId && l.OrganizationId == organization.Id && !l.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Product", command.ProductId);

        if (command.CategoryId.HasValue)
        {
            var category = await _unitOfWork.Repository<Category>().GetByIdAsync(command.CategoryId.Value, cancellationToken)
                ?? throw new NotFoundException("Category", command.CategoryId.Value);
            product.CategoryId = command.CategoryId.Value;
            product.Category = category;
        }

        if (command.Title != null) product.Title = command.Title;
        if (command.Description != null) product.Description = command.Description;

        var origPrice = command.OriginalPrice ?? product.OriginalPrice;
        var discPrice = command.DiscountedPrice ?? product.DiscountedPrice;

        if (origPrice < 0 || discPrice < 0)
        {
            throw new ArgumentException("Prices cannot be negative.");
        }

        if (discPrice > origPrice)
        {
            throw new ArgumentException("Discounted price cannot be greater than original price.");
        }

        product.OriginalPrice = origPrice;
        product.DiscountedPrice = discPrice;

        if (command.QuantityAvailable.HasValue)
        {
            if (command.QuantityAvailable.Value < 0)
            {
                throw new ArgumentException("Quantity available cannot be negative.");
            }
            product.QuantityAvailable = command.QuantityAvailable.Value;
        }

        if (command.ExpirationDate.HasValue)
        {
            product.ExpirationDate = command.ExpirationDate.Value;
        }

        if (!string.IsNullOrWhiteSpace(command.Status))
        {
            if (Enum.TryParse<ProductStatus>(command.Status, true, out var status))
            {
                product.Status = status;
            }
            else
            {
                throw new ArgumentException($"Invalid ProductStatus value: {command.Status}");
            }
        }

        product.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            command.OwnerId,
            organization.Id,
            "ProductUpdated",
            "Product Updated",
            $"Updated product details for '{product.Title}'.",
            null,
            cancellationToken);

        return product.ToDto();
    }
}




