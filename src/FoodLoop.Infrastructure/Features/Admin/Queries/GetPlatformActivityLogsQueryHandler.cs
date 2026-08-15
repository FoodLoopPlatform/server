using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin.Queries;

public class GetPlatformActivityLogsQueryHandler : IRequestHandler<GetPlatformActivityLogsQuery, IReadOnlyList<ActivityLogEntryDto>>
{
    private readonly ApplicationDbContext _db;

    public GetPlatformActivityLogsQueryHandler(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ActivityLogEntryDto>> Handle(GetPlatformActivityLogsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.EventType))
            query = query.Where(l => l.EventType == request.EventType);

        if (request.UserId.HasValue)
            query = query.Where(l => l.UserId == request.UserId.Value);

        if (request.OrganizationId.HasValue)
            query = query.Where(l => l.OrganizationId == request.OrganizationId.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(l => l.Title.ToLower().Contains(term) || l.Description.ToLower().Contains(term) || l.EventType.ToLower().Contains(term));
        }

        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // Batch load users and organizations for display names
        var userIds = logs.Where(l => l.UserId.HasValue).Select(l => l.UserId!.Value).Distinct().ToList();
        var orgIds = logs.Where(l => l.OrganizationId.HasValue).Select(l => l.OrganizationId!.Value).Distinct().ToList();

        var usersMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var orgsMap = await _db.Organizations
            .Where(o => orgIds.Contains(o.Id))
            .Select(o => new { o.Id, o.Name })
            .ToDictionaryAsync(o => o.Id, o => o.Name, cancellationToken);

        return logs.Select(l =>
        {
            var isAi = l.EventType.Contains("Ai", System.StringComparison.OrdinalIgnoreCase) || !l.UserId.HasValue;
            var actor = isAi ? "الذكاء الاصطناعي للنظام (System AI)" : (l.UserId.HasValue && usersMap.TryGetValue(l.UserId.Value, out var uName) ? uName : "System");
            
            var severity = "Low";
            if (l.EventType.Contains("Banned", System.StringComparison.OrdinalIgnoreCase) || 
                l.EventType.Contains("Deleted", System.StringComparison.OrdinalIgnoreCase) ||
                l.EventType.Contains("Dispute", System.StringComparison.OrdinalIgnoreCase))
            {
                severity = "High";
            }
            else if (l.EventType.Contains("Moderated", System.StringComparison.OrdinalIgnoreCase) ||
                     l.EventType.Contains("Reported", System.StringComparison.OrdinalIgnoreCase) ||
                     l.EventType.Contains("Status", System.StringComparison.OrdinalIgnoreCase))
            {
                severity = "Medium";
            }

            return new ActivityLogEntryDto
            {
                Id = l.Id,
                UserId = l.UserId,
                UserName = actor,
                ActorType = isAi ? "System AI" : "Admin",
                OrganizationId = l.OrganizationId,
                OrganizationName = l.OrganizationId.HasValue && orgsMap.TryGetValue(l.OrganizationId.Value, out var oName) ? oName : null,
                EventType = l.EventType,
                Title = l.Title,
                Description = l.Description,
                Severity = severity,
                IpAddress = l.IpAddress,
                OccurredAt = l.CreatedAt
            };
        }).ToList();
    }
}
