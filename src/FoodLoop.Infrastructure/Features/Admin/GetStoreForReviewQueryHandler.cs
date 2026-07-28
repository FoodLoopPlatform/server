using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Admin;

public class GetStoreForReviewQueryHandler : IRequestHandler<GetStoreForReviewQuery, AdminStoreDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;

    public GetStoreForReviewQueryHandler(IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    public async Task<AdminStoreDto> Handle(GetStoreForReviewQuery request, CancellationToken cancellationToken)
    {
        var store = await _unitOfWork.Stores.GetByIdWithVerificationsAsync(request.StoreId, cancellationToken)
            ?? throw new NotFoundException("Store", request.StoreId);

        var owner = await _userManager.FindByIdAsync(store.OwnerId.ToString())
            ?? throw new NotFoundException("User", store.OwnerId);

        return store.ToAdminDto(owner);
    }
}
