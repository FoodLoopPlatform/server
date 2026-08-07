using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Features.Organizations;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Products.Commands;

public class OcrScanCommandHandler : IRequestHandler<OcrScanCommand, OcrResultDto>
{
    private readonly ApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _storage;

    public OcrScanCommandHandler(ApplicationDbContext db, IUnitOfWork unitOfWork, IFileStorageService storage)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _storage = storage;
    }

    public async Task<OcrResultDto> Handle(OcrScanCommand request, CancellationToken cancellationToken)
    {
        var org = await _unitOfWork.FindByOwnerOrThrowAsync(request.OwnerId, "Organization not found.", cancellationToken);

        var product = await _db.Products
            .Include(p => p.AIRecognitionResult)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.OrganizationId == org.Id && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        // Save the uploaded image
        await _storage.SaveAsync(request.Image, $"ocr/{product.Id}", cancellationToken);

        // Simulate AI OCR analysis — in production swap with real AI service call
        var random = new Random(product.Id.GetHashCode());
        var confidence = Math.Round(0.55 + random.NextDouble() * 0.44, 2);
        var detectedName = product.Title;
        var extractedExpiry = product.ExpirationDate;

        var result = product.AIRecognitionResult;
        if (result == null)
        {
            result = new AIRecognitionResult
            {
                ProductId = product.Id,
                DetectedProduct = detectedName,
                ConfidenceScore = confidence,
                ExtractedExpiryDate = extractedExpiry,
                ExtractedText = $"Detected: {detectedName}. Expiry: {extractedExpiry}",
                Reviewed = false
            };
            _db.AIRecognitionResults.Add(result);
        }
        else
        {
            result.DetectedProduct = detectedName;
            result.ConfidenceScore = confidence;
            result.ExtractedExpiryDate = extractedExpiry;
            result.ExtractedText = $"Detected: {detectedName}. Expiry: {extractedExpiry}";
            result.Reviewed = false;
            _db.AIRecognitionResults.Update(result);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new OcrResultDto
        {
            ProductId = product.Id,
            DetectedProduct = result.DetectedProduct,
            ConfidenceScore = result.ConfidenceScore,
            ExtractedExpiryDate = result.ExtractedExpiryDate,
            ExtractedText = result.ExtractedText,
            Reviewed = result.Reviewed,
            ScannedAt = result.CreatedAt
        };
    }
}
