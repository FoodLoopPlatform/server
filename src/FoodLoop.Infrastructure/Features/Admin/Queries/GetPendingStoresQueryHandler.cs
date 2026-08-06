using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Admin.Queries;

public class GetPendingStoresQueryHandler : IRequestHandler<GetPendingStoresQuery, IReadOnlyList<AdminStoreDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;

    public GetPendingStoresQueryHandler(IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<AdminStoreDto>> Handle(GetPendingStoresQuery request, CancellationToken cancellationToken)
    {
        var stores = await _unitOfWork.Stores.GetByVerificationStatusAsync(
            VerificationStatus.Pending, cancellationToken);

        var result = new List<AdminStoreDto>();
        foreach (var store in stores)
        {
            var owner = await _userManager.FindByIdAsync(store.OwnerId.ToString());
            if (owner != null)
                result.Add(store.ToAdminDto(owner));
        }

        return result;
    }
}

