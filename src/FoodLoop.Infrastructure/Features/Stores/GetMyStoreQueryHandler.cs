using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Stores;
using FoodLoop.Application.Features.Stores.Queries;
using FoodLoop.Infrastructure.Mappings;
using MediatR;

namespace FoodLoop.Infrastructure.Features.Stores;

public class GetMyStoreQueryHandler : IRequestHandler<GetMyStoreQuery, StoreDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetMyStoreQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<StoreDto> Handle(GetMyStoreQuery query, CancellationToken cancellationToken)
    {
        var store = await _unitOfWork.FindByOwnerOrThrowAsync(query.OwnerId, cancellationToken);
        return store.ToDto();
    }
}
