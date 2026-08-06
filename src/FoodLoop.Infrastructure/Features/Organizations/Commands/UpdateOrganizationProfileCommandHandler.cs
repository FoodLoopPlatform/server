using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.Features.Organizations.Commands;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Organizations.Commands;

public class UpdateOrganizationProfileCommandHandler : IRequestHandler<UpdateOrganizationProfileCommand, OrganizationDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly ILocalizationService _loc;

    public UpdateOrganizationProfileCommandHandler(IUnitOfWork unitOfWork, IFileStorageService fileStorage, ILocalizationService loc)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _loc = loc;
    }

    public async Task<OrganizationDto> Handle(UpdateOrganizationProfileCommand command, CancellationToken cancellationToken)
    {
        var organization = await _unitOfWork.FindByOwnerOrThrowAsync(
            command.OwnerId, _loc["OrganizationNotFound"], cancellationToken);

        var req = command.Request;

        if (req.Name != null) organization.Name = req.Name.Trim();
        if (req.NameAr != null) organization.NameAr = req.NameAr.Trim();
        if (req.Description != null) organization.Description = req.Description;
        if (req.DescriptionAr != null) organization.DescriptionAr = req.DescriptionAr;
        if (req.BusinessCategory.HasValue) organization.BusinessCategory = req.BusinessCategory;

        if (req.LogoFile != null)
        {
            var logoUrl = await _fileStorage.SaveAsync(req.LogoFile, $"organizations/{organization.Id}", cancellationToken);
            organization.Logo = logoUrl;
        }
        else if (req.Logo != null)
        {
            organization.Logo = req.Logo;
        }

        if (req.Phone != null) organization.Phone = req.Phone;
        if (req.Email != null) organization.Email = req.Email;
        if (req.OpeningHours != null) organization.OpeningHours = req.OpeningHours;

        organization.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return organization.ToDto();
    }
}



