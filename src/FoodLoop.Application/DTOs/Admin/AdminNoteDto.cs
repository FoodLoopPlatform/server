namespace FoodLoop.Application.DTOs.Admin;

/// <summary>
/// Returned by POST and GET /admin/users/{id}/notes.
/// Represents a single admin note entry.
/// </summary>
public class AdminNoteDto
{
    public Guid Id { get; set; }

    public Guid SentByAdminId { get; set; }
    public string SentByAdminName { get; set; } = string.Empty;

    public Guid RecipientUserId { get; set; }
    public string RecipientName { get; set; } = string.Empty;

    /// <summary>Notice | Warning | Urgent | Internal</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Quick-template label, or null for custom notes.</summary>
    public string? Template { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>When true the note was not delivered to the user.</summary>
    public bool IsInternal { get; set; }

    public DateTimeOffset SentAt { get; set; }
}
