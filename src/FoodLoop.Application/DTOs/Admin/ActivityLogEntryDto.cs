namespace FoodLoop.Application.DTOs.Admin;

public class ActivityLogEntryDto
{
    public string EventType { get; set; } = string.Empty;   // AccountCreated, DocumentVerified, etc.
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}
