using FoodLoop.Application.DTOs.Reviews;
using FoodLoop.Application.Features.Reviews.Queries;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Reviews.Queries;

public class GetOrganizationReviewsQueryHandler : IRequestHandler<GetOrganizationReviewsQuery, IReadOnlyList<ReviewDto>>
{
    private readonly ApplicationDbContext _db;

    public GetOrganizationReviewsQueryHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ReviewDto>> Handle(GetOrganizationReviewsQuery request, CancellationToken cancellationToken)
    {
        var reviews = await _db.Reviews
            .Include(r => r.Organization)
            .Where(r => r.OrganizationId == request.OrganizationId)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var userIds = reviews.Select(r => r.UserId).Distinct().ToList();
        var users = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return reviews.Select(r => new ReviewDto
        {
            Id = r.Id,
            OrderId = r.OrderId,
            UserId = r.UserId,
            UserFullName = users.TryGetValue(r.UserId, out var name) ? name : string.Empty,
            OrganizationId = r.OrganizationId,
            OrganizationName = r.Organization?.Name ?? string.Empty,
            Rating = r.Rating,
            Comment = r.Comment,
            CreatedAt = r.CreatedAt
        }).ToList();
    }
}
