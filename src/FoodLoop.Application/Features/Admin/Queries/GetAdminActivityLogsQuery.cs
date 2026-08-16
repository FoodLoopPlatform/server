using FoodLoop.Application.DTOs.Admin;
using MediatR;

namespace FoodLoop.Application.Features.Admin.Queries;

/// <summary>
/// GET /admin/activity-logs/admin-actions
/// Returns a paginated, filterable feed of actions performed by admin users.
/// Filters the AuditLog to only rows whose EventType matches known admin-only
/// event types: DocumentVerified, UserStatusUpdated, DisputeResolved,
/// ProductModerated, ReviewModerated, SupportTicketClosed.
/// </summary>
public record GetAdminActivityLogsQuery(
    string? SearchTerm = null,
    string? EventType = null,
    Guid? AdminUserId = null,
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<AdminActivityLogsResultDto>;
