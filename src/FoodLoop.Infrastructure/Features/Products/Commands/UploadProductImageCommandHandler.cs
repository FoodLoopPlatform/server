using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Organizations;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Products.Commands;

public class UploadProductImageCommandHandler : IRequestHandler<UploadProductImageCommand, ProductDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly IAuditLogService _auditLogService;

    public UploadProductImageCommandHandler(
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorage,
        IAuditLogService auditLogService)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _auditLogService = auditLogService;
    }

    public async Task<ProductDto> Handle(UploadProductImageCommand command, CancellationToken cancellationToken)
    {
        var organization = await _unitOfWork.FindByOwnerOrThrowAsync(command.OwnerId, "Organization not found.", cancellationToken);

        var product = await _unitOfWork.Repository<Product>().Query()
            .Include(l => l.Category)
            .Include(l => l.Images)
            .Include(l => l.AIRecognitionResult)
            .FirstOrDefaultAsync(l => l.Id == command.ProductId && l.OrganizationId == organization.Id && !l.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Product", command.ProductId);

        var imageUrl = await _fileStorage.SaveAsync(command.File, $"listings/{product.Id}", cancellationToken);

        var displayOrder = product.Images.Any() ? product.Images.Max(i => i.DisplayOrder) + 1 : 0;

        var productImage = new ProductImage
        {
            ProductId = product.Id,
            ImageUrl = imageUrl,
            DisplayOrder = displayOrder
        };

        _unitOfWork.Repository<ProductImage>().Add(productImage);

        product.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            command.OwnerId,
            organization.Id,
            "ProductImageUploaded",
            "Product Image Uploaded",
            $"Uploaded image for product '{product.Title}'.",
            null,
            cancellationToken);

        return product.ToDto();
    }
}




