using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Organizations;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Products.Commands;

public class StatelessOcrScanCommandHandler : IRequestHandler<StatelessOcrScanCommand, OcrResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOcrService _ocrService;

    public StatelessOcrScanCommandHandler(IUnitOfWork unitOfWork, IOcrService ocrService)
    {
        _unitOfWork = unitOfWork;
        _ocrService = ocrService;
    }

    public async Task<OcrResultDto> Handle(StatelessOcrScanCommand request, CancellationToken cancellationToken)
    {
        var org = await _unitOfWork.FindByOwnerOrThrowAsync(request.OwnerId, "Organization not found.", cancellationToken);

        if (org.VerificationStatus != VerificationStatus.Verified)
        {
            throw new ArgumentException("Your organization must be verified by an admin before you can manage products.");
        }

        // Perform stateless AI Vision analysis via Google Gemini Vision
        var analysis = await _ocrService.AnalyzeProductImageAsync(
            request.File.Content,
            request.File.ContentType,
            cancellationToken);

        // Resolve suggested category from the database
        Guid? matchedCategoryId = null;
        string? matchedCategoryName = analysis.SuggestedCategory;

        if (!string.IsNullOrWhiteSpace(analysis.SuggestedCategory))
        {
            var categories = await _unitOfWork.Repository<Category>().Query().ToListAsync(cancellationToken);
            var matched = categories.FirstOrDefault(c => 
                c.Name.Equals(analysis.SuggestedCategory, StringComparison.OrdinalIgnoreCase) ||
                (c.NameAr != null && c.NameAr.Equals(analysis.SuggestedCategory, StringComparison.OrdinalIgnoreCase)) ||
                analysis.SuggestedCategory.Contains(c.Name, StringComparison.OrdinalIgnoreCase));
            
            if (matched != null)
            {
                matchedCategoryId = matched.Id;
                matchedCategoryName = matched.Name;
            }
        }

        return new OcrResultDto
        {
            DetectedProduct = analysis.DetectedProduct,
            SuggestedDescription = analysis.SuggestedDescription,
            SuggestedCategory = matchedCategoryName,
            SuggestedCategoryId = matchedCategoryId,
            ConfidenceScore = analysis.ConfidenceScore,
            ExtractedExpiryDate = analysis.ExpirationDate,
            ExtractedText = analysis.ExtractedText
        };
    }
}
