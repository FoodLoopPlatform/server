using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Products.Queries;
using FoodLoop.Infrastructure.Features.Organizations;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Products.Queries;

public class GetOcrResultQueryHandler : IRequestHandler<GetOcrResultQuery, OcrResultDto>
{
    private readonly ApplicationDbContext _db;

    public GetOcrResultQueryHandler(ApplicationDbContext db) => _db = db;

    public async Task<OcrResultDto> Handle(GetOcrResultQuery request, CancellationToken cancellationToken)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.OwnerId == request.OwnerId && !o.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Organization", request.OwnerId);

        var product = await _db.Products
            .Include(p => p.AIRecognitionResult)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.OrganizationId == org.Id && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        if (product.AIRecognitionResult == null)
            throw new NotFoundException("OCR result not found for this product. Submit an image first via POST /ocr.");

        var r = product.AIRecognitionResult;
        return new OcrResultDto
        {
            ProductId = product.Id,
            DetectedProduct = r.DetectedProduct ?? product.Title,
            SuggestedDescription = product.Description,
            SuggestedCategory = product.Category?.Name,
            SuggestedCategoryId = product.CategoryId != Guid.Empty ? product.CategoryId : null,
            ConfidenceScore = r.ConfidenceScore,
            ExtractedExpiryDate = r.ExtractedExpiryDate,
            ExtractedText = r.ExtractedText,
            Reviewed = r.Reviewed,
            ScannedAt = r.CreatedAt
        };
    }
}
