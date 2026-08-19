using FoodLoop.Domain.Common;

namespace FoodLoop.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // e.g. OrderConfirmed, PasswordChanged
    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
}
