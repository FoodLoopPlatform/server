using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Features.Organizations;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Products.Commands;

public class ApplyDiscountCommandHandler : IRequestHandler<ApplyDiscountCommand, ProductDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _audit;

    public ApplyDiscountCommandHandler(IUnitOfWork unitOfWork, IAuditLogService audit)
    {
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<ProductDto> Handle(ApplyDiscountCommand request, CancellationToken cancellationToken)
    {
        var org = await _unitOfWork.FindByOwnerOrThrowAsync(request.OwnerId, "Organization not found.", cancellationToken);

        var product = await _unitOfWork.Repository<Product>().Query()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.OrganizationId == org.Id && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        if (request.DiscountedPrice < 0)
            throw new ArgumentException("Discounted price cannot be negative.");
        if (request.DiscountedPrice > product.OriginalPrice)
            throw new ArgumentException("Discounted price cannot exceed original price.");

        // Record price history before mutating
        var history = new PriceHistory
        {
            ProductId = product.Id,
            OldOriginalPrice = product.OriginalPrice,
            OldDiscountedPrice = product.DiscountedPrice,
            NewOriginalPrice = product.OriginalPrice,
            NewDiscountedPrice = request.DiscountedPrice,
            ChangeReason = request.ChangeReason ?? "Manual discount",
            ChangedBy = request.OwnerId
        };
        _unitOfWork.Repository<PriceHistory>().Add(history);

        product.DiscountedPrice = request.DiscountedPrice;
        _unitOfWork.Repository<Product>().Update(product);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(request.OwnerId, org.Id, "DiscountApplied",
            "Discount Applied", $"Set discounted price to {request.DiscountedPrice} for '{product.Title}'.",
            null, cancellationToken);

        return product.ToDto();
    }
}
