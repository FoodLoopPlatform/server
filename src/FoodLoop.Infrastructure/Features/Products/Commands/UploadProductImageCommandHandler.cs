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
    private readonly IOcrService _ocrService;

    public UploadProductImageCommandHandler(
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorage,
        IAuditLogService auditLogService,
        IOcrService ocrService)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _auditLogService = auditLogService;
        _ocrService = ocrService;
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

        using var memoryStream = new MemoryStream();
        await command.File.Content.CopyToAsync(memoryStream, cancellationToken);
        var imageBytes = memoryStream.ToArray();

        string imageUrl;
        using (var storageStream = new MemoryStream(imageBytes))
        {
            var fileUploadRequest = new FileUploadRequest
            {
                FileName = command.File.FileName,
                ContentType = command.File.ContentType,
                Content = storageStream
            };
            imageUrl = await _fileStorage.SaveAsync(fileUploadRequest, $"listings/{product.Id}", cancellationToken);
        }

        var displayOrder = product.Images.Any() ? product.Images.Max(i => i.DisplayOrder) + 1 : 0;

        var productImage = new ProductImage
        {
            ProductId = product.Id,
            ImageUrl = imageUrl,
            DisplayOrder = displayOrder
        };

        _unitOfWork.Repository<ProductImage>().Add(productImage);

        // Run real AI OCR scanner on the first uploaded image
        if (product.AIRecognitionResult == null)
        {
            using var ocrStream = new MemoryStream(imageBytes);
            var analysis = await _ocrService.AnalyzeProductImageAsync(
                ocrStream,
                command.File.ContentType,
                cancellationToken);

            var detectedName = !string.IsNullOrWhiteSpace(analysis.DetectedProduct) ? analysis.DetectedProduct : product.Title;
            var extractedExpiry = analysis.ExpirationDate ?? product.ExpirationDate;
            var confidence = analysis.ConfidenceScore;
            var extractedText = analysis.ExtractedText;

            var aiResult = new AIRecognitionResult
            {
                ProductId = product.Id,
                DetectedProduct = detectedName,
                ConfidenceScore = confidence,
                ExtractedExpiryDate = extractedExpiry,
                ExtractedText = extractedText,
                Reviewed = false
            };

            _unitOfWork.Repository<AIRecognitionResult>().Add(aiResult);
            product.AIRecognitionResult = aiResult;
            product.ExpiryVerificationState = confidence < 0.8 ? ExpiryVerificationState.AiLowConfidence : ExpiryVerificationState.AiVerified;

            // Auto-fill empty or placeholder fields on the product
            if (product.Title == "New Product" || product.Title == "Draft Product" || string.IsNullOrWhiteSpace(product.Title))
            {
                product.Title = detectedName;
            }

            if (string.IsNullOrWhiteSpace(product.Description))
            {
                product.Description = analysis.SuggestedDescription;
            }

            if (product.ExpirationDate == default || product.ExpirationDate == DateOnly.FromDateTime(DateTime.Today))
            {
                if (analysis.ExpirationDate.HasValue)
                {
                    product.ExpirationDate = analysis.ExpirationDate.Value;
                }
            }

            // Resolve and auto-fill category if needed
            if (product.CategoryId == Guid.Empty && !string.IsNullOrWhiteSpace(analysis.SuggestedCategory))
            {
                var categories = await _unitOfWork.Repository<Category>().Query().ToListAsync(cancellationToken);
                var matched = categories.FirstOrDefault(c => 
                    c.Name.Equals(analysis.SuggestedCategory, StringComparison.OrdinalIgnoreCase) ||
                    c.NameAr.Equals(analysis.SuggestedCategory, StringComparison.OrdinalIgnoreCase) ||
                    analysis.SuggestedCategory.Contains(c.Name, StringComparison.OrdinalIgnoreCase));
                
                if (matched != null)
                {
                    product.CategoryId = matched.Id;
                    product.Category = matched;
                }
            }

            // If confidence is low, set to PendingModeration; otherwise set Active
            if (confidence < 0.8)
            {
                product.Status = ProductStatus.PendingModeration;
            }
            else
            {
                product.Status = ProductStatus.Active;
            }
        }

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




