using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Application.Common.Interfaces;

public interface IFirebasePushNotificationService
{
    Task SendToUserAsync(
        Guid userId,
        string title,
        string body,
        string type,
        CancellationToken cancellationToken = default);
}
