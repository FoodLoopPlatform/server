using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Stores;
using FoodLoop.Application.Features.Stores.Commands;
using FoodLoop.Infrastructure.Mappings;
using MediatR;

namespace FoodLoop.Infrastructure.Features.Stores;

public class UpdateStoreProfileCommandHandler : IRequestHandler<UpdateStoreProfileCommand, StoreDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _loc;

    public UpdateStoreProfileCommandHandler(IUnitOfWork unitOfWork, ILocalizationService loc)
    {
        _unitOfWork = unitOfWork;
        _loc = loc;
    }

    public async Task<StoreDto> Handle(UpdateStoreProfileCommand command, CancellationToken cancellationToken)
    {
        var store = await _unitOfWork.FindByOwnerOrThrowAsync(
            command.OwnerId, _loc["StoreNotFound"], cancellationToken);

        var req = command.Request;

        if (req.Name != null) store.Name = req.Name.Trim();
        if (req.NameAr != null) store.NameAr = req.NameAr.Trim();
        if (req.Description != null) store.Description = req.Description;
        if (req.DescriptionAr != null) store.DescriptionAr = req.DescriptionAr;
        if (req.BusinessCategory.HasValue) store.BusinessCategory = req.BusinessCategory;
        if (req.Logo != null) store.Logo = req.Logo;

        store.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return store.ToDto();
    }
}
