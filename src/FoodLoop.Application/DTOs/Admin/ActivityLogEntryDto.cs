using System;

namespace FoodLoop.Application.DTOs.Admin;

public class ActivityLogEntryDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string ActorType { get; set; } = "User"; // "System AI", "Admin", "Merchant", "Customer", "Charity"
    public Guid? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "Low"; // "Low", "Medium", "High"
    public string? IpAddress { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
