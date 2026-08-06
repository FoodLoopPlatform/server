using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Listings;
using FoodLoop.Application.Features.Listings.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Organizations;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Listings.Queries;

public class GetMyProductsQueryHandler : IRequestHandler<GetMyProductsQuery, IReadOnlyList<ProductDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetMyProductsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ProductDto>> Handle(GetMyProductsQuery query, CancellationToken cancellationToken)
    {
        var organization = await _unitOfWork.FindByOwnerOrThrowAsync(query.OwnerId, "Organization not found.", cancellationToken);

        var dbQuery = _unitOfWork.Repository<Product>().Query()
            .Include(l => l.Category)
            .Include(l => l.Images)
            .Where(l => l.OrganizationId == organization.Id && !l.IsDeleted)
            .AsQueryable();

        if (query.CategoryId.HasValue)
        {
            dbQuery = dbQuery.Where(l => l.CategoryId == query.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (Enum.TryParse<ProductStatus>(query.Status, true, out var status))
            {
                dbQuery = dbQuery.Where(l => l.Status == status);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var search = query.SearchTerm.Trim().ToLower();
            dbQuery = dbQuery.Where(l => l.Title.ToLower().Contains(search) || (l.TitleAr != null && l.TitleAr.ToLower().Contains(search)));
        }

        var products = await dbQuery
            .OrderByDescending(l => l.CreatedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return products.Select(l => l.ToDto()).ToList();
    }
}


