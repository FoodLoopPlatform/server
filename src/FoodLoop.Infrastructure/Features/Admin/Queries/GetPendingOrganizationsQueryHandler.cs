using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Admin.Queries;

public class GetPendingOrganizationsQueryHandler : IRequestHandler<GetPendingOrganizationsQuery, IReadOnlyList<AdminOrganizationDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;

    public GetPendingOrganizationsQueryHandler(IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<AdminOrganizationDto>> Handle(GetPendingOrganizationsQuery request, CancellationToken cancellationToken)
    {
        var organizations = await _unitOfWork.Organizations.GetByVerificationStatusAsync(
            VerificationStatus.Pending, cancellationToken);

        var result = new List<AdminOrganizationDto>();
        foreach (var organization in organizations)
        {
            var owner = await _userManager.FindByIdAsync(organization.OwnerId.ToString());
            if (owner != null)
                result.Add(organization.ToAdminDto(owner));
        }

        return result;
    }
}



