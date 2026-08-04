using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Stores;
using FoodLoop.Application.Features.Stores.Commands;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Stores;

public class UpdateStoreProfileCommandHandler : IRequestHandler<UpdateStoreProfileCommand, StoreDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly ILocalizationService _loc;

    public UpdateStoreProfileCommandHandler(IUnitOfWork unitOfWork, IFileStorageService fileStorage, ILocalizationService loc)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
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

        if (req.LogoFile != null)
        {
            var logoUrl = await _fileStorage.SaveAsync(req.LogoFile, $"stores/{store.Id}", cancellationToken);
            store.Logo = logoUrl;
        }
        else if (req.Logo != null)
        {
            store.Logo = req.Logo;
        }

        if (req.Phone != null) store.Phone = req.Phone;
        if (req.Email != null) store.Email = req.Email;
        if (req.OpeningHours != null) store.OpeningHours = req.OpeningHours;

        store.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return store.ToDto();
    }
}
