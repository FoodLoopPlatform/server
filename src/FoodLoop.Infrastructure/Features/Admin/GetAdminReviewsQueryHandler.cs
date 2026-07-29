using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin;

public class GetAdminReviewsQueryHandler : IRequestHandler<GetAdminReviewsQuery, IReadOnlyList<AdminReviewDto>>
{
    private readonly ApplicationDbContext _context;

    public GetAdminReviewsQueryHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AdminReviewDto>> Handle(GetAdminReviewsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Reviews
            .Include(r => r.Store)
            .AsQueryable();

        if (request.Rating.HasValue)
        {
            query = query.Where(r => r.Rating == request.Rating.Value);
        }

        if (request.StoreId.HasValue)
        {
            query = query.Where(r => r.StoreId == request.StoreId.Value);
        }

        var reviews = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var userIds = reviews.Select(r => r.UserId).Distinct().ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return reviews.Select(r => new AdminReviewDto
        {
            Id = r.Id,
            OrderId = r.OrderId,
            UserId = r.UserId,
            CustomerName = users.TryGetValue(r.UserId, out var name) ? name : "Unknown Customer",
            StoreId = r.StoreId,
            StoreName = r.Store?.Name ?? "Unknown Store",
            Rating = r.Rating,
            Comment = r.Comment,
            CreatedAt = r.CreatedAt
        }).ToList();
    }
}
