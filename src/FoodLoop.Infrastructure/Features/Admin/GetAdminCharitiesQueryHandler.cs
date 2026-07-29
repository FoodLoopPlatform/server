using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin;

public class GetAdminCharitiesQueryHandler : IRequestHandler<GetAdminCharitiesQuery, IReadOnlyList<AdminStoreDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;

    public GetAdminCharitiesQueryHandler(IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<AdminStoreDto>> Handle(GetAdminCharitiesQuery request, CancellationToken cancellationToken)
    {
        var charityUsers = await _userManager.GetUsersInRoleAsync(AppRole.Charity);
        var charityUserIds = charityUsers.Select(u => u.Id).ToList();

        var query = _unitOfWork.Stores.Query()
            .Include(s => s.Verifications)
            .Where(s => !s.IsDeleted && charityUserIds.Contains(s.OwnerId))
            .AsQueryable();

        if (request.Status.HasValue)
        {
            query = query.Where(s => s.VerificationStatus == request.Status.Value);
        }

        var stores = await query
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var result = new List<AdminStoreDto>();
        foreach (var store in stores)
        {
            var owner = charityUsers.FirstOrDefault(u => u.Id == store.OwnerId);
            if (owner != null)
                result.Add(store.ToAdminDto(owner));
        }

        return result;
    }
}
