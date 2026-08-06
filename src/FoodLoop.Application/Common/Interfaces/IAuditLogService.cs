using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Application.Common.Interfaces;

public interface IAuditLogService
{
    Task LogAsync(
        Guid? userId,
        Guid? organizationId,
        string eventType,
        string title,
        string description,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);
}
