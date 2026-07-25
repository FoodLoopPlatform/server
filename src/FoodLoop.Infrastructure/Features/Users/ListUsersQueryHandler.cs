using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.Users.Queries;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Features.Users;

public class ListUsersQueryHandler : IRequestHandler<ListUsersQuery, IReadOnlyList<UserDto>>
{
    private readonly ApplicationDbContext _context;

    public ListUsersQueryHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<UserDto>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Users.AsQueryable();

        // 1. Filter by Role
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            query = from user in query
                    join userRole in _context.Set<IdentityUserRole<Guid>>() on user.Id equals userRole.UserId
                    join role in _context.Roles on userRole.RoleId equals role.Id
                    where role.Name == request.Role
                    select user;
        }

        // 2. Filter by Status
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<UserStatus>(request.Status, true, out var userStatus))
        {
            query = query.Where(u => u.Status == userStatus);
        }

        // 3. Search Term (Name, Email, Phone)
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.Trim();
            query = query.Where(u => u.FullName.Contains(search) || 
                                     (u.Email != null && u.Email.Contains(search)) || 
                                     (u.PhoneNumber != null && u.PhoneNumber.Contains(search)));
        }

        // 4. Paginate
        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 10;

        var pagedUsers = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 5. Fetch Roles efficiently in one query to avoid N+1 queries
        var pagedUserIds = pagedUsers.Select(u => u.Id).ToList();
        var userRoles = await (from userRole in _context.Set<IdentityUserRole<Guid>>()
                               join role in _context.Roles on userRole.RoleId equals role.Id
                               where pagedUserIds.Contains(userRole.UserId)
                               select new { userRole.UserId, RoleName = role.Name })
                              .ToListAsync(cancellationToken);

        var rolesLookup = userRoles
            .GroupBy(ur => ur.UserId)
            .ToDictionary(g => g.Key, g => g.Select(ur => ur.RoleName).ToList());

        var userDtos = pagedUsers.Select(u =>
        {
            var roles = rolesLookup.TryGetValue(u.Id, out var r) ? r : new List<string>();
            return u.ToDto(roles);
        }).ToList();

        return userDtos;
    }
}
