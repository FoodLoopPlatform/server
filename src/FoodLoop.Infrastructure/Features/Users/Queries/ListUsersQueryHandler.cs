using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.Users.Queries;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Mappings;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Features.Users.Queries;

public class ListUsersQueryHandler : IRequestHandler<ListUsersQuery, PagedResult<UserDto>>
{
    private readonly ApplicationDbContext _context;

    public ListUsersQueryHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<UserDto>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Users.AsQueryable();

        // Filter by Role
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            query = from user in query
                    join userRole in _context.Set<IdentityUserRole<Guid>>() on user.Id equals userRole.UserId
                    join role in _context.Roles on userRole.RoleId equals role.Id
                    where role.Name == request.Role
                    select user;
        }

        // Filter by Status
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<UserStatus>(request.Status, true, out var userStatus))
        {
            query = query.Where(u => u.Status == userStatus);
        }

        // Search Term (Name, Email, Phone)
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.Trim();
            query = query.Where(u =>
                u.FullName.Contains(search) ||
                (u.Email != null && u.Email.Contains(search)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize is > 0 and <= 100 ? request.PageSize : 10;

        var pagedUsers = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Fetch roles in one query to avoid N+1
        var userIds = pagedUsers.Select(u => u.Id).ToList();
        var userRoles = await (from ur in _context.Set<IdentityUserRole<Guid>>()
                               join r in _context.Roles on ur.RoleId equals r.Id
                               where userIds.Contains(ur.UserId)
                               select new { ur.UserId, RoleName = r.Name })
                              .ToListAsync(cancellationToken);

        var rolesLookup = userRoles
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).ToList());

        var items = pagedUsers.Select(u =>
        {
            var roles = rolesLookup.TryGetValue(u.Id, out var r) ? r : new List<string>();
            return u.ToDto(roles);
        }).ToList();

        return new PagedResult<UserDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }
}

