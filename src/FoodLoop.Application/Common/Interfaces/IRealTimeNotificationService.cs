using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Application.Common.Interfaces;

public interface IRealTimeNotificationService
{
    Task SendNotificationToUserAsync(
        Guid userId,
        string title,
        string body,
        string type,
        CancellationToken cancellationToken = default);

    Task SendNotificationToUserAsync(
        Guid userId,
        string title,
        string body,
        string type,
        string? entityType,
        Guid? entityId,
        CancellationToken cancellationToken = default);

    Task SendNotificationToRoleAsync(
        string roleName,
        string title,
        string body,
        string type,
        string? entityType = null,
        Guid? entityId = null,
        CancellationToken cancellationToken = default);
}
