using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Organizations;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Products.Commands;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, ProductDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;

    public CreateProductCommandHandler(IUnitOfWork unitOfWork, IAuditLogService auditLogService)
    {
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
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

        var state = command.ExpiryVerificationState ?? ExpiryVerificationState.Manual;
        var status = state == ExpiryVerificationState.AiLowConfidence ? ProductStatus.PendingModeration : ProductStatus.Active;

        var product = new Product
        {
            OrganizationId = organization.Id,
            CategoryId = command.CategoryId,
            Title = command.Title,
            Description = command.Description,
            OriginalPrice = command.OriginalPrice,
            DiscountedPrice = command.DiscountedPrice,
            QuantityAvailable = command.QuantityAvailable,
            ExpirationDate = command.ExpirationDate,
            ExpiryVerificationState = state,
            Status = status
        };

        _unitOfWork.Repository<Product>().Add(product);

        var history = new PriceHistory
        {
            ProductId = product.Id,
            OldOriginalPrice = 0,
            OldDiscountedPrice = 0,
            NewOriginalPrice = product.OriginalPrice,
            NewDiscountedPrice = product.DiscountedPrice,
            ChangeReason = "Initial listing",
            ChangedBy = command.OwnerId
        };
        _unitOfWork.Repository<PriceHistory>().Add(history);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (state == ExpiryVerificationState.AiVerified || state == ExpiryVerificationState.AiLowConfidence)
        {
            var aiResult = new AIRecognitionResult
            {
                ProductId = product.Id,
                DetectedProduct = product.Title,
                ConfidenceScore = command.OcrConfidence ?? 0.0,
                ExtractedExpiryDate = product.ExpirationDate,
                ExtractedText = command.OcrText,
                Reviewed = false
            };
            _unitOfWork.Repository<AIRecognitionResult>().Add(aiResult);
            product.AIRecognitionResult = aiResult;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await _auditLogService.LogAsync(
            command.OwnerId,
            organization.Id,
            "ProductListed",
            "Product Listed",
            $"Listed new product '{product.Title}'.",
            null,
            cancellationToken);

        // Fetch category info to populate DTO
        product.Category = category;

        return product.ToDto();
    }
}




