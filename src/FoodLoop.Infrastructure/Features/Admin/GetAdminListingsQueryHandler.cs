using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin;

public class GetAdminListingsQueryHandler : IRequestHandler<GetAdminListingsQuery, IReadOnlyList<AdminListingDto>>
{
    private readonly ApplicationDbContext _context;

    public GetAdminListingsQueryHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AdminListingDto>> Handle(GetAdminListingsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.ProductListings
            .Include(l => l.Store)
            .Include(l => l.Category)
            .Where(l => !l.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ListingStatus>(request.Status, true, out var status))
        {
            query = query.Where(l => l.Status == status);
        }

        if (request.StoreId.HasValue)
        {
            query = query.Where(l => l.StoreId == request.StoreId.Value);
        }

        var listings = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return listings.Select(l => new AdminListingDto
        {
            Id = l.Id,
            StoreId = l.StoreId,
            StoreName = l.Store?.Name ?? "Unknown Store",
            CategoryId = l.CategoryId,
            CategoryName = l.Category?.Name ?? "Unknown Category",
            Title = l.Title,
            TitleAr = l.TitleAr,
            OriginalPrice = l.OriginalPrice,
            DiscountedPrice = l.DiscountedPrice,
            QuantityAvailable = l.QuantityAvailable,
            ExpirationDate = l.ExpirationDate,
            Status = l.Status.ToString(),
            CreatedAt = l.CreatedAt
        }).ToList();
    }
}
