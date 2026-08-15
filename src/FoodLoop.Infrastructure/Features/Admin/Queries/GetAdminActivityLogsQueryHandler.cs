using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Features.Admin.Queries;

/// <summary>
/// Handler for GET /admin/activity-logs/admin-actions.
///
/// Strategy: the AuditLog table stores every system event. Admin-performed events
/// are identified by their EventType, which is one of the known admin action types:
///   DocumentVerified, UserStatusUpdated, DisputeResolved,
///   ProductModerated, ReviewModerated, SupportTicketClosed.
///
/// We filter on these EventTypes to isolate the "admin actions" feed.
/// Pagination, date range, free-text search, and per-admin filtering are all
/// supported. User names are batch-loaded to avoid N+1 queries.
/// </summary>
public class GetAdminActivityLogsQueryHandler
    : IRequestHandler<GetAdminActivityLogsQuery, AdminActivityLogsResultDto>
{
    /// <summary>
    /// The EventTypes that represent deliberate admin actions.
    /// These match the strings written by each Admin command handler.
    /// </summary>
    private static readonly HashSet<string> AdminEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "DocumentVerified",    // VerifyOrganizationCommandHandler
        "UserStatusUpdated",   // UpdateUserStatusCommandHandler
        "DisputeResolved",     // ResolveDisputeCommandHandler
        "ProductModerated",    // ModerateProductCommandHandler
        "ReviewModerated",     // DeleteReviewCommandHandler
        "SupportTicketClosed", // CloseSupportTicketCommandHandler
    };

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public GetAdminActivityLogsQueryHandler(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<AdminActivityLogsResultDto> Handle(
        GetAdminActivityLogsQuery request, CancellationToken cancellationToken)
    {
        // 1. Start from AuditLogs filtered to admin event types
        var query = _db.AuditLogs
            .AsNoTracking()
            .Where(l => AdminEventTypes.Contains(l.EventType));

        // 2. Optional: filter to a specific admin user
        if (request.AdminUserId.HasValue)
            query = query.Where(l => l.UserId == request.AdminUserId.Value);

        // 3. Optional: filter by specific event type
        if (!string.IsNullOrWhiteSpace(request.EventType))
            query = query.Where(l => l.EventType == request.EventType);

        // 4. Optional: date range filter
        if (request.DateFrom.HasValue)
            query = query.Where(l => l.CreatedAt >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(l => l.CreatedAt <= request.DateTo.Value);

        // 5. Optional: free-text search on title and description
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(l =>
                l.Title.ToLower().Contains(term) ||
                l.Description.ToLower().Contains(term) ||
                l.EventType.ToLower().Contains(term));
        }

        // 6. Count total before paging (for pagination metadata)
        var totalCount = await query.CountAsync(cancellationToken);

        // 7. Fetch the page
        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // 8. Batch-load admin user names to avoid N+1 queries
        var adminUserIds = logs
            .Where(l => l.UserId.HasValue)
            .Select(l => l.UserId!.Value)
            .Distinct()
            .ToList();

        var adminNamesMap = await _db.Users
            .Where(u => adminUserIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        // 9. Batch-load organization names
        var orgIds = logs
            .Where(l => l.OrganizationId.HasValue)
            .Select(l => l.OrganizationId!.Value)
            .Distinct()
            .ToList();

        var orgNamesMap = await _db.Organizations
            .Where(o => orgIds.Contains(o.Id))
            .Select(o => new { o.Id, o.Name })
            .ToDictionaryAsync(o => o.Id, o => o.Name, cancellationToken);

        // 10. Map to DTOs, deriving severity from EventType
        var items = logs.Select(l =>
        {
            var adminInfo = l.UserId.HasValue && adminNamesMap.TryGetValue(l.UserId.Value, out var a)
                ? a
                : null;

            var severity = l.EventType switch
            {
                "UserStatusUpdated" => "High",
                "DisputeResolved"   => "High",
                "ReviewModerated"   => "Medium",
                "ProductModerated"  => "Medium",
                "DocumentVerified"  => "Low",
                "SupportTicketClosed" => "Low",
                _ => "Low"
            };

            return new ActivityLogEntryDto
            {
                Id            = l.Id,
                UserId        = l.UserId,
                UserName      = adminInfo?.FullName ?? "Admin",
                ActorType     = "Admin",
                OrganizationId   = l.OrganizationId,
                OrganizationName = l.OrganizationId.HasValue && orgNamesMap.TryGetValue(l.OrganizationId.Value, out var orgName)
                    ? orgName : null,
                EventType   = l.EventType,
                Title       = l.Title,
                Description = l.Description,
                Severity    = severity,
                IpAddress   = l.IpAddress,
                OccurredAt  = l.CreatedAt
            };
        }).ToList();

        return new AdminActivityLogsResultDto
        {
            Items      = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize   = request.PageSize
        };
    }
}
