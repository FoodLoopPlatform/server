using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.Features.Organizations.Commands;
using FoodLoop.Infrastructure.Mappings;
using MediatR;

namespace FoodLoop.Infrastructure.Features.Organizations.Commands;

public class UpdateOrganizationLocationCommandHandler : IRequestHandler<UpdateOrganizationLocationCommand, OrganizationDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _loc;

    public UpdateOrganizationLocationCommandHandler(IUnitOfWork unitOfWork, ILocalizationService loc)
    {
        _unitOfWork = unitOfWork;
        _loc = loc;
    }

    public async Task<OrganizationDto> Handle(UpdateOrganizationLocationCommand command, CancellationToken cancellationToken)
    {
        var organization = await _unitOfWork.FindByOwnerOrThrowAsync(command.OwnerId, _loc["OrganizationNotFound"], cancellationToken);
        var request = command.Request;

        organization.Governorate = request.Governorate;
        organization.City = request.City;
        organization.Neighborhood = request.Neighborhood;
        organization.Street = request.Street;
        organization.BuildingNo = request.BuildingNo;
        organization.Latitude = request.Latitude;
        organization.Longitude = request.Longitude;
        organization.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return organization.ToDto();
    }
}



