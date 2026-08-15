using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.Features.Organizations.Queries;
using FoodLoop.Infrastructure.Mappings;
using MediatR;

namespace FoodLoop.Infrastructure.Features.Organizations.Queries;

public class GetMyStoreQueryHandler : IRequestHandler<GetMyOrganizationQuery, OrganizationDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _loc;

    public GetMyStoreQueryHandler(IUnitOfWork unitOfWork, ILocalizationService loc)
    {
        _unitOfWork = unitOfWork;
        _loc = loc;
    }

    public async Task<OrganizationDto> Handle(GetMyOrganizationQuery query, CancellationToken cancellationToken)
    {
        var organization = await _unitOfWork.FindByOwnerOrThrowAsync(query.OwnerId, _loc["StoreNotFound"], cancellationToken);
        return organization.ToDto();
    }
}


