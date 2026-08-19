using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Notifications;
using FoodLoop.Application.Features.Notifications.Queries;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Notifications.Queries;

public class GetNotificationByIdQueryHandler : IRequestHandler<GetNotificationByIdQuery, NotificationDto>
{
    private readonly ApplicationDbContext _db;

    public GetNotificationByIdQueryHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<NotificationDto> Handle(GetNotificationByIdQuery request, CancellationToken cancellationToken)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId && n.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Notification", request.NotificationId);

        return new NotificationDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            Title = notification.Title,
            Body = notification.Body,
            Type = notification.Type,
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            EntityType = notification.EntityType,
            EntityId = notification.EntityId,
            CreatedAt = notification.CreatedAt
        };
    }
}
