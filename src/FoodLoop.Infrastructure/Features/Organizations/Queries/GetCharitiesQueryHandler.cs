using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.Features.Organizations.Queries;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FoodLoop.Infrastructure.Identity;

namespace FoodLoop.Infrastructure.Features.Organizations.Queries;

public class GetCharitiesQueryHandler : IRequestHandler<GetCharitiesQuery, IReadOnlyList<CharityDto>>
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public GetCharitiesQueryHandler(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<CharityDto>> Handle(GetCharitiesQuery request, CancellationToken cancellationToken)
    {
        // Get all user IDs that are in the Charity role
        var charityUsers = await _userManager.GetUsersInRoleAsync(AppRole.Charity);
        var charityUserIds = charityUsers.Select(u => u.Id).ToHashSet();

        var charities = await _db.Organizations
            .Where(o => !o.IsDeleted
                && o.VerificationStatus == VerificationStatus.Verified
                && charityUserIds.Contains(o.OwnerId))
            .OrderBy(o => o.Name)
            .Select(o => new CharityDto
            {
                Id = o.Id,
                Name = o.Name,
                NameAr = o.NameAr,
                Description = o.Description,
                DescriptionAr = o.DescriptionAr,
                Logo = o.Logo,
                City = o.City,
                Phone = o.Phone,
                Email = o.Email,
                AverageRating = o.AverageRating
            })
            .ToListAsync(cancellationToken);

        return charities;
    }
}
