using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Notifications;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Hubs;
using FoodLoop.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Services;

public class RealTimeNotificationService : IRealTimeNotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;
    private readonly IFirebasePushNotificationService _firebasePushNotificationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RealTimeNotificationService> _logger;

    public RealTimeNotificationService(
        ApplicationDbContext db,
        IHubContext<NotificationHub, INotificationHubClient> hubContext,
        IFirebasePushNotificationService firebasePushNotificationService,
        ILogger<RealTimeNotificationService> logger)
        : this(db, hubContext, firebasePushNotificationService, null!, logger)
    {
    }

    public RealTimeNotificationService(
        ApplicationDbContext db,
        IHubContext<NotificationHub, INotificationHubClient> hubContext,
        IFirebasePushNotificationService firebasePushNotificationService,
        UserManager<ApplicationUser> userManager,
        ILogger<RealTimeNotificationService> logger)
    {
        _db = db;
        _hubContext = hubContext;
        _firebasePushNotificationService = firebasePushNotificationService;
        _userManager = userManager;
        _logger = logger;
    }

    public Task SendNotificationToUserAsync(
        Guid userId,
        string title,
        string body,
        string type,
        CancellationToken cancellationToken = default)
    {
        return SendNotificationToUserAsync(userId, title, body, type, null, null, cancellationToken);
    }

    public async Task SendNotificationToUserAsync(
        Guid userId,
        string title,
        string body,
        string type,
        string? entityType,
        Guid? entityId,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Body = body,
            Type = type,
            IsRead = false,
            EntityType = entityType,
            EntityId = entityId
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = new NotificationDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            Title = notification.Title,
            Body = notification.Body,
            Type = notification.Type,
            IsRead = notification.IsRead,
            EntityType = notification.EntityType,
            EntityId = notification.EntityId,
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

    public async Task SendNotificationToRoleAsync(
        string roleName,
        string title,
        string body,
        string type,
        string? entityType = null,
        Guid? entityId = null,
        CancellationToken cancellationToken = default)
    {
        var users = await _userManager.GetUsersInRoleAsync(roleName);
        if (users == null || users.Count == 0)
        {
            return;
        }

        foreach (var user in users)
        {
            Notification notification;
            try
            {
                notification = new Notification
                {
                    UserId = user.Id,
                    Title = title,
                    Body = body,
                    Type = type,
                    IsRead = false,
                    EntityType = entityType,
                    EntityId = entityId
                };

                _db.Notifications.Add(notification);
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DATABASE SAVE FAILED: Failed to persist notification record for recipient user {UserId} during role broadcast {RoleName}", user.Id, roleName);
                continue;
            }

            var dto = new NotificationDto
            {
                Id = notification.Id,
                UserId = notification.UserId,
                Title = notification.Title,
                Body = notification.Body,
                Type = notification.Type,
                IsRead = notification.IsRead,
                EntityType = notification.EntityType,
                EntityId = notification.EntityId,
                CreatedAt = notification.CreatedAt
            };

            try
            {
                // Push real-time SignalR message to the specific user group.
                await _hubContext.Clients.User(user.Id.ToString()).ReceiveNotification(dto);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DELIVERY FAILED: Notification persisted but failed to dispatch real-time message (SignalR/FCM) to user {UserId} during role broadcast {RoleName}", user.Id, roleName);
            }

            try
            {
                // Firebase push.
                await _firebasePushNotificationService.SendToUserAsync(user.Id, title, body, type, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DELIVERY FAILED: Notification persisted but failed to dispatch real-time message (SignalR/FCM) to user {UserId} during role broadcast {RoleName}", user.Id, roleName);
            }
        }
    }
}
