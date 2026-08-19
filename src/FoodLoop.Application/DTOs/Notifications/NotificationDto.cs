using System;

namespace FoodLoop.Application.DTOs.Notifications;

public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
