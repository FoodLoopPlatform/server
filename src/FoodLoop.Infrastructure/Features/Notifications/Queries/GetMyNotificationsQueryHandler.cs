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
        var notifications = await _db.Notifications
            .Where(n => n.UserId == request.UserId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);

        return notifications.Select(n => new NotificationDto
        {
            Id = n.Id,
            Title = n.Title,
            Body = n.Body,
            Type = n.Type,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        }).ToList();
    }
}
