using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Admin.Queries;

public class GetStoreForReviewQueryHandler : IRequestHandler<GetStoreForReviewQuery, AdminOrganizationDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;

    public GetStoreForReviewQueryHandler(IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    public async Task<AdminOrganizationDto> Handle(GetStoreForReviewQuery request, CancellationToken cancellationToken)
    {
        var organization = await _unitOfWork.Organizations.GetByIdWithVerificationsAsync(request.OrganizationId, cancellationToken)
            ?? throw new NotFoundException("Organization", request.OrganizationId);

        var owner = await _userManager.FindByIdAsync(organization.OwnerId.ToString())
            ?? throw new NotFoundException("User", organization.OwnerId);

        return organization.ToAdminDto(owner);
    }
}


