using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Stores;
using FoodLoop.Application.Features.Stores.Queries;
using FoodLoop.Infrastructure.Mappings;
using MediatR;

namespace FoodLoop.Infrastructure.Features.Stores;

public class GetMyStoreQueryHandler : IRequestHandler<GetMyStoreQuery, StoreDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _loc;

    public GetMyStoreQueryHandler(IUnitOfWork unitOfWork, ILocalizationService loc)
    {
        _unitOfWork = unitOfWork;
        _loc = loc;
    }

    public async Task<StoreDto> Handle(GetMyStoreQuery query, CancellationToken cancellationToken)
    {
        var store = await _unitOfWork.FindByOwnerOrThrowAsync(query.OwnerId, _loc["StoreNotFound"], cancellationToken);
        return store.ToDto();
    }
}
