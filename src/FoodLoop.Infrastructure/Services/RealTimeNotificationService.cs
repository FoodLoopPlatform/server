using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Notifications;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Services;

public class RealTimeNotificationService : IRealTimeNotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;
    private readonly IFirebasePushNotificationService _firebasePushNotificationService;
    private readonly ILogger<RealTimeNotificationService> _logger;

    public RealTimeNotificationService(
        ApplicationDbContext db,
        IHubContext<NotificationHub, INotificationHubClient> hubContext,
        IFirebasePushNotificationService firebasePushNotificationService,
        ILogger<RealTimeNotificationService> logger)
    {
        _db = db;
        _hubContext = hubContext;
        _firebasePushNotificationService = firebasePushNotificationService;
        _logger = logger;
    }

    public async Task SendNotificationToUserAsync(
        Guid userId,
        string title,
        string body,
        string type,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Body = body,
            Type = type,
            IsRead = false
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = new NotificationDto
        {
            Id = notification.Id,
            Title = notification.Title,
            Body = notification.Body,
            Type = notification.Type,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt
        };

        try
        {
            // Push real-time SignalR message to the specific user connection group.
            await _hubContext.Clients.User(userId.ToString()).ReceiveNotification(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch SignalR real-time notification to user {UserId}.", userId);
        }

        try
        {
            // Best-practice hybrid delivery: web via SignalR, mobile via Firebase push.
            await _firebasePushNotificationService.SendToUserAsync(userId, title, body, type, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch Firebase push notification to user {UserId}.", userId);
        }
    }
}
