using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Products.Queries;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Products.Queries;

public class GetPriceHistoryQueryHandler : IRequestHandler<GetPriceHistoryQuery, IReadOnlyList<PriceHistoryDto>>
{
    private readonly ApplicationDbContext _db;

    public GetPriceHistoryQueryHandler(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<PriceHistoryDto>> Handle(GetPriceHistoryQuery request, CancellationToken cancellationToken)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.OwnerId == request.OwnerId && !o.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Organization", request.OwnerId);

        var product = await _db.Products.FirstOrDefaultAsync(
            p => p.Id == request.ProductId && p.OrganizationId == org.Id && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        var history = await _db.PriceHistories
            .Where(h => h.ProductId == request.ProductId)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new PriceHistoryDto
            {
                Id = h.Id,
                ProductId = h.ProductId,
                OldOriginalPrice = h.OldOriginalPrice,
                OldDiscountedPrice = h.OldDiscountedPrice,
                NewOriginalPrice = h.NewOriginalPrice,
                NewDiscountedPrice = h.NewDiscountedPrice,
                ChangeReason = h.ChangeReason,
                ChangedBy = h.ChangedBy,
                CreatedAt = h.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return history;
    }
}
