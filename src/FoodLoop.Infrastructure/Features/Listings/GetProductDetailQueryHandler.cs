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

public class GetProductDetailQueryHandler : IRequestHandler<GetProductDetailQuery, ProductDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetProductDetailQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ProductDto> Handle(GetProductDetailQuery query, CancellationToken cancellationToken)
    {
        var store = await _unitOfWork.FindByOwnerOrThrowAsync(query.OwnerId, "Store not found.", cancellationToken);

        var product = await _unitOfWork.Repository<Product>().Query()
            .Include(l => l.Category)
            .Include(l => l.Images)
            .FirstOrDefaultAsync(l => l.Id == query.ProductId && l.StoreId == store.Id && !l.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Product", query.ProductId);

        return product.ToDto();
    }
}
