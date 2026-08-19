using FoodLoop.Application.DTOs.Notifications;
using FoodLoop.Application.Features.Notifications.Queries;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Notifications.Queries;

public class GetMyNotificationsQueryHandler : IRequestHandler<GetMyNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    private readonly ApplicationDbContext _db;

    public GetMyNotificationsQueryHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<NotificationDto>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Notifications
            .Where(n => n.UserId == request.UserId);

        if (request.IsRead.HasValue)
        {
            query = query.Where(n => n.IsRead == request.IsRead.Value);
        }

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return notifications.Select(n => new NotificationDto
        {
            Id = n.Id,
            UserId = n.UserId,
            Title = n.Title,
            Body = n.Body,
            Type = n.Type,
            IsRead = n.IsRead,
            ReadAt = n.ReadAt,
            EntityType = n.EntityType,
            EntityId = n.EntityId,
            CreatedAt = n.CreatedAt
        }).ToList();
    }
}
