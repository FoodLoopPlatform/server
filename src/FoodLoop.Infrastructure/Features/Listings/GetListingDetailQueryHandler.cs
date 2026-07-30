using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Listings;
using FoodLoop.Application.Features.Listings.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Features.Stores;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Listings;

public class GetListingDetailQueryHandler : IRequestHandler<GetListingDetailQuery, ProductListingDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetListingDetailQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ProductListingDto> Handle(GetListingDetailQuery query, CancellationToken cancellationToken)
    {
        var store = await _unitOfWork.FindByOwnerOrThrowAsync(query.OwnerId, "Store not found.", cancellationToken);

        var listing = await _unitOfWork.Repository<ProductListing>().Query()
            .Include(l => l.Category)
            .Include(l => l.Images)
            .FirstOrDefaultAsync(l => l.Id == query.ListingId && l.StoreId == store.Id && !l.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ProductListing", query.ListingId);

        return listing.ToDto();
    }
}
