using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Products.Queries;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Products.Queries;

public class GetMarketplaceProductDetailQueryHandler : IRequestHandler<GetMarketplaceProductDetailQuery, MarketplaceProductDto>
{
    private readonly ApplicationDbContext _db;

    public GetMarketplaceProductDetailQueryHandler(ApplicationDbContext db) => _db = db;

    public async Task<MarketplaceProductDto> Handle(GetMarketplaceProductDetailQuery request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && !p.IsDeleted && p.Status == ProductStatus.Active, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        return new MarketplaceProductDto
        {
            Id = product.Id,
            OrganizationId = product.OrganizationId,
            OrganizationName = product.Organization?.Name ?? string.Empty,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name ?? string.Empty,
            Title = product.Title,
            Description = product.Description,
            OriginalPrice = product.OriginalPrice,
            DiscountedPrice = product.DiscountedPrice,
            QuantityAvailable = product.QuantityAvailable,
            ExpirationDate = product.ExpirationDate,
            ExpiryVerificationState = product.ExpiryVerificationState.ToString(),
            Status = product.Status.ToString(),
            Latitude = product.Organization?.Latitude,
            Longitude = product.Organization?.Longitude,
            DistanceKm = null,
            Images = product.Images
                .OrderBy(i => i.DisplayOrder)
                .Select(i => new ProductImageDto { Id = i.Id, ImageUrl = i.ImageUrl, DisplayOrder = i.DisplayOrder })
                .ToArray()
        };
    }
}
