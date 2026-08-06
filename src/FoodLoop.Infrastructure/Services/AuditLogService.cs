using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly IUnitOfWork _unitOfWork;

    public AuditLogService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task LogAsync(
        Guid? userId,
        Guid? organizationId,
        string eventType,
        string title,
        string description,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var log = new AuditLog
        {
            UserId = userId,
            OrganizationId = organizationId,
            EventType = eventType,
            Title = title,
            Description = description,
            IpAddress = ipAddress
        };

        _unitOfWork.Repository<AuditLog>().Add(log);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
