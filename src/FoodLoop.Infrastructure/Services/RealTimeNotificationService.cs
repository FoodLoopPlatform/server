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
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<RealTimeNotificationService> _logger;

    public RealTimeNotificationService(
        ApplicationDbContext db,
        IHubContext<NotificationHub, INotificationHubClient> hubContext,
        IFirebasePushNotificationService firebasePushNotificationService,
        ILocalizationService localizationService,
        ILogger<RealTimeNotificationService> logger)
        : this(db, hubContext, firebasePushNotificationService, null!, localizationService, logger)
    {
    }

    public RealTimeNotificationService(
        ApplicationDbContext db,
        IHubContext<NotificationHub, INotificationHubClient> hubContext,
        IFirebasePushNotificationService firebasePushNotificationService,
        UserManager<ApplicationUser> userManager,
        ILocalizationService localizationService,
        ILogger<RealTimeNotificationService> logger)
    {
        _db = db;
        _hubContext = hubContext;
        _firebasePushNotificationService = firebasePushNotificationService;
        _userManager = userManager;
        _localizationService = localizationService;
        _logger = logger;
    }

    public Task SendNotificationToUserAsync(
        Guid userId,
        string titleKey,
        string bodyKey,
        string type,
        object[] bodyArgs,
        CancellationToken cancellationToken = default)
    {
        return SendNotificationToUserAsync(userId, titleKey, bodyKey, type, bodyArgs, null, null, cancellationToken);
    }

    public async Task SendNotificationToUserAsync(
        Guid userId,
        string titleKey,
        string bodyKey,
        string type,
        object[] bodyArgs,
        string? entityType,
        Guid? entityId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FindAsync(new object[] { userId }, cancellationToken);
        var lang = user?.Language ?? "en";

        string title;
        string body;

        using (new CultureScope(lang))
        {
            title = _localizationService[titleKey];
            body = _localizationService[bodyKey, bodyArgs];
        }

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
            await _hubContext.Clients.User(userId.ToString()).ReceiveNotification(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch SignalR real-time notification to user {UserId}.", userId);
        }

        try
        {
            await _firebasePushNotificationService.SendToUserAsync(userId, title, body, type, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch Firebase push notification to user {UserId}.", userId);
        }
    }

    public async Task SendNotificationToRoleAsync(
        string roleName,
        string titleKey,
        string bodyKey,
        string type,
        object[] bodyArgs,
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
            string title;
            string body;

            using (new CultureScope(user.Language ?? "en"))
            {
                title = _localizationService[titleKey];
                body = _localizationService[bodyKey, bodyArgs];
            }

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
                await _hubContext.Clients.User(user.Id.ToString()).ReceiveNotification(dto);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DELIVERY FAILED: Notification persisted but failed to dispatch real-time message (SignalR/FCM) to user {UserId} during role broadcast {RoleName}", user.Id, roleName);
            }

            try
            {
                await _firebasePushNotificationService.SendToUserAsync(user.Id, title, body, type, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DELIVERY FAILED: Notification persisted but failed to dispatch real-time message (SignalR/FCM) to user {UserId} during role broadcast {RoleName}", user.Id, roleName);
            }
        }
    }
}

public class CultureScope : IDisposable
{
    private readonly System.Globalization.CultureInfo _originalCulture;
    private readonly System.Globalization.CultureInfo _originalUiCulture;

    public CultureScope(string cultureName)
    {
        _originalCulture = System.Globalization.CultureInfo.CurrentCulture;
        _originalUiCulture = System.Globalization.CultureInfo.CurrentUICulture;

        var culture = new System.Globalization.CultureInfo(cultureName);
        System.Globalization.CultureInfo.CurrentCulture = culture;
        System.Globalization.CultureInfo.CurrentUICulture = culture;
    }

    public void Dispose()
    {
        System.Globalization.CultureInfo.CurrentCulture = _originalCulture;
        System.Globalization.CultureInfo.CurrentUICulture = _originalUiCulture;
    }
}
