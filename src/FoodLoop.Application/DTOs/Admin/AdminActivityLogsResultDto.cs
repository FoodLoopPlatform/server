namespace FoodLoop.Application.DTOs.Admin;

/// <summary>
/// Paginated result wrapper for admin activity logs.
/// Returned by GET /admin/activity-logs/admin-actions.
/// </summary>
public class AdminActivityLogsResultDto
{
    public IReadOnlyList<ActivityLogEntryDto> Items { get; set; } = Array.Empty<ActivityLogEntryDto>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}
