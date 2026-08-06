using FoodLoop.Domain.Common;
using System;

namespace FoodLoop.Domain.Entities;

/// <summary>
/// Represents an immutable log entry of a system activity or security-sensitive event.
/// </summary>
public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
}
