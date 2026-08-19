using FoodLoop.Application.DTOs.Notifications;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.Notifications.Commands;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Notifications.Commands;

public class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, Result<NotificationDto>>
{
    private readonly ApplicationDbContext _db;

    public MarkNotificationReadCommandHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<NotificationDto>> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == request.NotificationId, cancellationToken)
            ?? throw new NotFoundException("Notification", request.NotificationId);

        if (notification.UserId != request.UserId)
        {
            return Result<NotificationDto>.Fail("Unauthorized access to modify this notification.");
        }

        notification.IsRead = true;
        notification.ReadAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Result<NotificationDto>.Ok(new NotificationDto
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
        });
    }
}
