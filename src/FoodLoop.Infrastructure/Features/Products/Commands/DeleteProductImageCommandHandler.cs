using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Features.Organizations;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Products.Commands;

public class DeleteProductImageCommandHandler : IRequestHandler<DeleteProductImageCommand, ProductDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;

    public DeleteProductImageCommandHandler(IUnitOfWork unitOfWork, IAuditLogService auditLogService)
    {
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
    }

    public async Task<ProductDto> Handle(DeleteProductImageCommand command, CancellationToken cancellationToken)
    {
        var organization = await _unitOfWork.FindByOwnerOrThrowAsync(command.OwnerId, "Organization not found.", cancellationToken);

        var product = await _unitOfWork.Repository<Product>().Query()
            .Include(l => l.Category)
            .Include(l => l.Images)
            .FirstOrDefaultAsync(l => l.Id == command.ProductId && l.OrganizationId == organization.Id && !l.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Product", command.ProductId);

        var image = product.Images.FirstOrDefault(i => i.Id == command.ImageId)
            ?? throw new NotFoundException("ProductImage", command.ImageId);

        _unitOfWork.Repository<ProductImage>().Remove(image);
        product.Images.Remove(image);
        product.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            command.OwnerId,
            organization.Id,
            "ProductImageDeleted",
            "Product Image Removed",
            $"Removed image for product '{product.Title}'.",
            null,
            cancellationToken);

        return product.ToDto();
    }
}




