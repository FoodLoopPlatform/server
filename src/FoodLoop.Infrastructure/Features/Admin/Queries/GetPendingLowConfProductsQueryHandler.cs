using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin.Queries;

public class GetPendingLowConfProductsQueryHandler
    : IRequestHandler<GetPendingLowConfProductsQuery, IReadOnlyList<AdminProductDto>>
{
    private readonly ApplicationDbContext _context;

    public GetPendingLowConfProductsQueryHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AdminProductDto>> Handle(
        GetPendingLowConfProductsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Products
            .Include(l => l.Organization)
            .Include(l => l.Category)
            .Include(l => l.AIRecognitionResult)
            .Where(l => !l.IsDeleted && l.Status == ProductStatus.PendingModeration)
            .Where(l => l.AIRecognitionResult == null || l.AIRecognitionResult.ConfidenceScore < request.ConfidenceThreshold)
            .AsQueryable();

        var products = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return products.Select(l => new AdminProductDto
        {
            Id = l.Id,
            OrganizationId = l.OrganizationId,
            StoreName = l.Organization?.Name ?? string.Empty,
            CategoryId = l.CategoryId,
            CategoryName = l.Category?.Name ?? string.Empty,
            Title = l.Title,
            OriginalPrice = l.OriginalPrice,
            DiscountedPrice = l.DiscountedPrice,
            QuantityAvailable = l.QuantityAvailable,
            ExpirationDate = l.ExpirationDate,
            ExpiryVerificationState = l.ExpiryVerificationState.ToString(),
            Status = l.Status.ToString(),
            AIConfidenceScore = l.AIRecognitionResult?.ConfidenceScore,
            ModerationNote = l.ModerationNote,
            CreatedAt = l.CreatedAt
        }).ToList();
    }
}


