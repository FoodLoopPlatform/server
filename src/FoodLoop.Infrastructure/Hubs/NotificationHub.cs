using FoodLoop.Application.DTOs.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Hubs;

[Authorize]
public class NotificationHub : Hub<INotificationHubClient>
{
}

public interface INotificationHubClient
{
    Task ReceiveNotification(NotificationDto notification);
}
