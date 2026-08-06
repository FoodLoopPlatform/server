using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Products.Queries;
using FoodLoop.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Products.Queries;

public class GetMarketplaceProductsQueryHandler : IRequestHandler<GetMarketplaceProductsQuery, IReadOnlyList<MarketplaceProductDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMarketplaceProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MarketplaceProductDto>> Handle(GetMarketplaceProductsQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Fetch candidates: Active, not expired, quantity > 0, organization is verified, not deleted
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Organization)
            .Where(p => !p.IsDeleted 
                && p.Status == ProductStatus.Active 
                && p.ExpirationDate >= today 
                && p.QuantityAvailable > 0
                && p.Organization != null
                && !p.Organization.IsDeleted
                && p.Organization.VerificationStatus == VerificationStatus.Verified);

        // Apply Search Filter (Title, Description, or Organization Name)
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.Trim().ToLower();
            query = query.Where(p => p.Title.ToLower().Contains(search) 
                || (p.TitleAr != null && p.TitleAr.ToLower().Contains(search))
                || (p.Description != null && p.Description.ToLower().Contains(search))
                || (p.DescriptionAr != null && p.DescriptionAr.ToLower().Contains(search))
                || p.Organization!.Name.ToLower().Contains(search));
        }

        // Apply Category Filter
        if (request.CategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == request.CategoryId.Value);
        }

        // Apply Price Filter (on DiscountedPrice)
        if (request.MinPrice.HasValue)
        {
            query = query.Where(p => p.DiscountedPrice >= request.MinPrice.Value);
        }
        if (request.MaxPrice.HasValue)
        {
            query = query.Where(p => p.DiscountedPrice <= request.MaxPrice.Value);
        }

        var products = await query.ToListAsync(cancellationToken);

        // Map to Marketplace DTO and calculate distance
        var mapped = products.Select(p =>
        {
            double? distance = null;
            if (request.UserLatitude.HasValue && request.UserLongitude.HasValue && p.Organization?.Latitude != null && p.Organization?.Longitude != null)
            {
                distance = CalculateDistance(
                    request.UserLatitude.Value,
                    request.UserLongitude.Value,
                    p.Organization.Latitude.Value,
                    p.Organization.Longitude.Value);
            }

            return new MarketplaceProductDto
            {
                Id = p.Id,
                OrganizationId = p.OrganizationId,
                OrganizationName = p.Organization?.Name ?? string.Empty,
                CategoryId = p.CategoryId,
                CategoryName = p.Category?.Name ?? string.Empty,
                Title = p.Title,
                TitleAr = p.TitleAr,
                Description = p.Description,
                DescriptionAr = p.DescriptionAr,
                OriginalPrice = p.OriginalPrice,
                DiscountedPrice = p.DiscountedPrice,
                QuantityAvailable = p.QuantityAvailable,
                ExpirationDate = p.ExpirationDate,
                Status = p.Status.ToString(),
                Latitude = p.Organization?.Latitude,
                Longitude = p.Organization?.Longitude,
                DistanceKm = distance,
                Images = p.Images.Select(img => new ProductImageDto
                {
                    Id = img.Id,
                    ImageUrl = img.ImageUrl,
                    DisplayOrder = img.DisplayOrder
                }).OrderBy(i => i.DisplayOrder).ToList()
            };
        });

        // Apply Distance filter
        if (request.MaxDistanceKm.HasValue && request.UserLatitude.HasValue && request.UserLongitude.HasValue)
        {
            mapped = mapped.Where(dto => dto.DistanceKm.HasValue && dto.DistanceKm.Value <= request.MaxDistanceKm.Value);
        }

        // Apply Sorting
        mapped = request.SortBy?.ToLowerInvariant() switch
        {
            "distance" => mapped.OrderBy(p => p.DistanceKm ?? double.MaxValue),
            "price_asc" => mapped.OrderBy(p => p.DiscountedPrice),
            "price_desc" => mapped.OrderByDescending(p => p.DiscountedPrice),
            "discount" => mapped.OrderByDescending(p => p.OriginalPrice > 0 ? (p.OriginalPrice - p.DiscountedPrice) / p.OriginalPrice : 0),
            "expiration" => mapped.OrderBy(p => p.ExpirationDate),
            _ => mapped.OrderBy(p => p.DistanceKm ?? double.MaxValue) // Default sort by distance if user coordinates are provided
        };

        // Apply Pagination
        var resultList = mapped
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return resultList;
    }

    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        var r = 6371; // Earth's radius in km
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
        return r * c;
    }

    private static double ToRadians(double val) => (Math.PI / 180) * val;
}
