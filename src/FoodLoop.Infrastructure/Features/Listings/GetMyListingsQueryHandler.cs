using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Listings;
using FoodLoop.Application.Features.Listings.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Stores;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Listings;

public class GetMyListingsQueryHandler : IRequestHandler<GetMyListingsQuery, IReadOnlyList<ProductListingDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetMyListingsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ProductListingDto>> Handle(GetMyListingsQuery query, CancellationToken cancellationToken)
    {
        var store = await _unitOfWork.FindByOwnerOrThrowAsync(query.OwnerId, "Store not found.", cancellationToken);

        var dbQuery = _unitOfWork.Repository<ProductListing>().Query()
            .Include(l => l.Category)
            .Include(l => l.Images)
            .Where(l => l.StoreId == store.Id && !l.IsDeleted)
            .AsQueryable();

        if (query.CategoryId.HasValue)
        {
            dbQuery = dbQuery.Where(l => l.CategoryId == query.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (Enum.TryParse<ListingStatus>(query.Status, true, out var status))
            {
                dbQuery = dbQuery.Where(l => l.Status == status);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var search = query.SearchTerm.Trim().ToLower();
            dbQuery = dbQuery.Where(l => l.Title.ToLower().Contains(search) || (l.TitleAr != null && l.TitleAr.ToLower().Contains(search)));
        }

        var listings = await dbQuery
            .OrderByDescending(l => l.CreatedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return listings.Select(l => l.ToDto()).ToList();
    }
}
