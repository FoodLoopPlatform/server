using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.Features.Organizations.Commands;
using FoodLoop.Infrastructure.Mappings;
using MediatR;

namespace FoodLoop.Infrastructure.Features.Organizations.Commands;

public class UpdateStoreLocationCommandHandler : IRequestHandler<UpdateOrganizationLocationCommand, OrganizationDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _loc;
    private readonly IAuditLogService _auditLogService;

    public UpdateStoreLocationCommandHandler(IUnitOfWork unitOfWork, ILocalizationService loc, IAuditLogService auditLogService)
    {
        _unitOfWork = unitOfWork;
        _loc = loc;
        _auditLogService = auditLogService;
    }

    public async Task<OrganizationDto> Handle(UpdateOrganizationLocationCommand command, CancellationToken cancellationToken)
    {
        var organization = await _unitOfWork.FindByOwnerOrThrowAsync(command.OwnerId, _loc["StoreNotFound"], cancellationToken);
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

        await _auditLogService.LogAsync(
            command.OwnerId,
            organization.Id,
            "StoreProfileUpdated",
            "Organization Profile Updated",
            $"Updated organization settings, opening hours, or location coordinates for '{organization.Name}'.",
            null,
            cancellationToken);

        return organization.ToDto();
    }
}


