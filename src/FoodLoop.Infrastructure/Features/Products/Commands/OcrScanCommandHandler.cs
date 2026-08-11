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
    private readonly IOcrService _ocrService;
    private readonly IAuditLogService _auditLogService;

    public OcrScanCommandHandler(
        ApplicationDbContext db,
        IUnitOfWork unitOfWork,
        IFileStorageService storage,
        IOcrService ocrService,
        IAuditLogService auditLogService)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _storage = storage;
        _ocrService = ocrService;
        _auditLogService = auditLogService;
    }

    public async Task<OcrResultDto> Handle(OcrScanCommand request, CancellationToken cancellationToken)
    {
        var org = await _unitOfWork.FindByOwnerOrThrowAsync(request.OwnerId, "Organization not found.", cancellationToken);

        var product = await _db.Products
            .Include(p => p.AIRecognitionResult)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.OrganizationId == org.Id && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        // 1. Buffer the image stream so it can be read multiple times without ObjectDisposedException
        using var memoryStream = new MemoryStream();
        await request.Image.Content.CopyToAsync(memoryStream, cancellationToken);
        var imageBytes = memoryStream.ToArray();

        using (var storageStream = new MemoryStream(imageBytes))
        {
            var storageRequest = new FoodLoop.Application.Common.Models.FileUploadRequest
            {
                FileName = request.Image.FileName,
                ContentType = request.Image.ContentType,
                Content = storageStream
            };
            await _storage.SaveAsync(storageRequest, $"ocr/{product.Id}", cancellationToken);
        }

        // 2. Perform real AI Vision analysis via Google Gemini Vision
        using var ocrStream = new MemoryStream(imageBytes);
        var analysis = await _ocrService.AnalyzeProductImageAsync(
            ocrStream,
            request.Image.ContentType,
            cancellationToken);

        var detectedName = !string.IsNullOrWhiteSpace(analysis.DetectedProduct) ? analysis.DetectedProduct : product.Title;
        var extractedExpiry = analysis.ExpirationDate ?? product.ExpirationDate;
        var confidence = analysis.ConfidenceScore;
        var extractedText = analysis.ExtractedText;

        var result = product.AIRecognitionResult;
        if (result == null)
        {
            result = new AIRecognitionResult
            {
                ProductId = product.Id,
                DetectedProduct = detectedName,
                ConfidenceScore = confidence,
                ExtractedExpiryDate = extractedExpiry,
                ExtractedText = extractedText,
                Reviewed = false
            };
            _db.AIRecognitionResults.Add(result);
        }
        else
        {
            result.DetectedProduct = detectedName;
            result.ConfidenceScore = confidence;
            result.ExtractedExpiryDate = extractedExpiry;
            result.ExtractedText = extractedText;
            result.Reviewed = false;
            _db.AIRecognitionResults.Update(result);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // 3. Log audit event
        await _auditLogService.LogAsync(
            request.OwnerId,
            org.Id,
            "ProductOcrScanned",
            "AI Vision Packaging Scanned",
            $"Scanned product packaging for '{product.Title}'. Detected: '{detectedName}', Confidence: {confidence:P0}.",
            null,
            cancellationToken);

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
