using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Application.Common.Interfaces;

public interface IRealTimeNotificationService
{
    Task SendNotificationToUserAsync(
        Guid userId,
        string titleKey,
        string bodyKey,
        string type,
        object[] bodyArgs,
        CancellationToken cancellationToken = default);

    Task SendNotificationToUserAsync(
        Guid userId,
        string titleKey,
        string bodyKey,
        string type,
        object[] bodyArgs,
        string? entityType,
        Guid? entityId,
        CancellationToken cancellationToken = default);

    Task SendNotificationToRoleAsync(
        string roleName,
        string titleKey,
        string bodyKey,
        string type,
        object[] bodyArgs,
        string? entityType = null,
        Guid? entityId = null,
        CancellationToken cancellationToken = default);
}
