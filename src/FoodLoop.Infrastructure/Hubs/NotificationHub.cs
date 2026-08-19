using FoodLoop.Application.DTOs.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Hubs;

[Authorize]
public class NotificationHub : Hub<INotificationHubClient>
{
    public override async Task OnConnectedAsync()
    {
        if (Context.User != null && Context.User.IsInRole("Admin"))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Admin");
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.User != null && Context.User.IsInRole("Admin"))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Admin");
        }
        await base.OnDisconnectedAsync(exception);
    }
}

public interface INotificationHubClient
{
    Task ReceiveNotification(NotificationDto notification);
}
