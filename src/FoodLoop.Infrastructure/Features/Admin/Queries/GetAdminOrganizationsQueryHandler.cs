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

namespace FoodLoop.Infrastructure.Features.Admin.Queries;

public class GetAdminOrganizationsQueryHandler : IRequestHandler<GetAdminOrganizationsQuery, IReadOnlyList<AdminOrganizationDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;

    public GetAdminOrganizationsQueryHandler(IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<AdminOrganizationDto>> Handle(GetAdminOrganizationsQuery request, CancellationToken cancellationToken)
    {
        var merchantUsers = await _userManager.GetUsersInRoleAsync(AppRole.Merchant);
        var merchantUserIds = merchantUsers.Select(u => u.Id).ToList();

        var query = _unitOfWork.Organizations.Query()
            .Include(s => s.Verifications)
            .Where(s => !s.IsDeleted && merchantUserIds.Contains(s.OwnerId))
            .AsQueryable();

        if (request.Status.HasValue)
        {
            query = query.Where(s => s.VerificationStatus == request.Status.Value);
        }

        var organizations = await query
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var result = new List<AdminOrganizationDto>();
        foreach (var organization in organizations)
        {
            var owner = merchantUsers.FirstOrDefault(u => u.Id == organization.OwnerId);
            if (owner != null)
                result.Add(organization.ToAdminDto(owner));
        }

        return result;
    }
}



