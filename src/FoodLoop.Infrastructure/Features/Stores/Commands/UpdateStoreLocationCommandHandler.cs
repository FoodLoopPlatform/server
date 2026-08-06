using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Stores;
using FoodLoop.Application.Features.Stores.Commands;
using FoodLoop.Infrastructure.Mappings;
using MediatR;

namespace FoodLoop.Infrastructure.Features.Stores.Commands;

public class UpdateStoreLocationCommandHandler : IRequestHandler<UpdateStoreLocationCommand, StoreDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _loc;

    public UpdateStoreLocationCommandHandler(IUnitOfWork unitOfWork, ILocalizationService loc)
    {
        _unitOfWork = unitOfWork;
        _loc = loc;
    }

    public async Task<StoreDto> Handle(UpdateStoreLocationCommand command, CancellationToken cancellationToken)
    {
        var store = await _unitOfWork.FindByOwnerOrThrowAsync(command.OwnerId, _loc["StoreNotFound"], cancellationToken);
        var request = command.Request;

        store.Governorate = request.Governorate;
        store.City = request.City;
        store.Neighborhood = request.Neighborhood;
        store.Street = request.Street;
        store.BuildingNo = request.BuildingNo;
        store.Latitude = request.Latitude;
        store.Longitude = request.Longitude;
        store.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return store.ToDto();
    }
}

