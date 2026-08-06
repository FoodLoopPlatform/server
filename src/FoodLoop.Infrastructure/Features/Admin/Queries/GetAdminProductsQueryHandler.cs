using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin.Queries;

public class GetAdminProductsQueryHandler : IRequestHandler<GetAdminProductsQuery, IReadOnlyList<AdminProductDto>>
{
    private readonly ApplicationDbContext _context;

    public GetAdminProductsQueryHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AdminProductDto>> Handle(GetAdminProductsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Products
            .Include(l => l.Store)
            .Include(l => l.Category)
            .Include(l => l.AIRecognitionResult)
            .Where(l => !l.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ProductStatus>(request.Status, true, out var status))
        {
            query = query.Where(l => l.Status == status);
        }

        if (request.StoreId.HasValue)
        {
            query = query.Where(l => l.StoreId == request.StoreId.Value);
        }

        var products = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return products.Select(l => new AdminProductDto
        {
            Id = l.Id,
            StoreId = l.StoreId,
            StoreName = l.Store?.Name ?? string.Empty,
            CategoryId = l.CategoryId,
            CategoryName = l.Category?.Name ?? string.Empty,
            Title = l.Title,
            TitleAr = l.TitleAr,
            OriginalPrice = l.OriginalPrice,
            DiscountedPrice = l.DiscountedPrice,
            QuantityAvailable = l.QuantityAvailable,
            ExpirationDate = l.ExpirationDate,
            Status = l.Status.ToString(),
            AIConfidenceScore = l.AIRecognitionResult?.ConfidenceScore,
            ModerationNote = l.ModerationNote,
            CreatedAt = l.CreatedAt
        }).ToList();
    }
}

