using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin.Queries;

public class GetActivityLogByIdQueryHandler : IRequestHandler<GetActivityLogByIdQuery, ActivityLogEntryDto>
{
    private readonly ApplicationDbContext _db;

    public GetActivityLogByIdQueryHandler(ApplicationDbContext db) => _db = db;

    public async Task<ActivityLogEntryDto> Handle(GetActivityLogByIdQuery request, CancellationToken cancellationToken)
    {
        var log = await _db.AuditLogs.AsNoTracking().FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(AuditLog), request.Id);

        string? userName = null;
        if (log.UserId.HasValue)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == log.UserId.Value, cancellationToken);
            userName = user?.FullName;
        }

        string? orgName = null;
        if (log.OrganizationId.HasValue)
        {
            var org = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == log.OrganizationId.Value, cancellationToken);
            orgName = org?.Name;
        }

        var isAi = log.EventType.Contains("Ai", StringComparison.OrdinalIgnoreCase) || !log.UserId.HasValue;
        var actor = isAi ? "System AI v4.2" : (userName ?? "System");

        var severity = "Low";
        if (log.EventType.Contains("Banned", StringComparison.OrdinalIgnoreCase) || 
            log.EventType.Contains("Deleted", StringComparison.OrdinalIgnoreCase) ||
            log.EventType.Contains("Dispute", StringComparison.OrdinalIgnoreCase))
        {
            severity = "High";
        }
        else if (log.EventType.Contains("Moderated", StringComparison.OrdinalIgnoreCase) ||
                 log.EventType.Contains("Reported", StringComparison.OrdinalIgnoreCase) ||
                 log.EventType.Contains("Status", StringComparison.OrdinalIgnoreCase))
        {
            severity = "Medium";
        }

        return new ActivityLogEntryDto
        {
            Id = log.Id,
            UserId = log.UserId,
            UserName = actor,
            ActorType = isAi ? "System AI" : "Admin",
            OrganizationId = log.OrganizationId,
            OrganizationName = orgName,
            EventType = log.EventType,
            Title = log.Title,
            Description = log.Description,
            Severity = severity,
            IpAddress = log.IpAddress,
            OccurredAt = log.CreatedAt
        };
    }
}
