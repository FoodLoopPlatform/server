using FoodLoop.Domain.Common;

namespace FoodLoop.Domain.Entities;

/// <summary>
/// A note or message sent by an admin to a specific user.
/// Admins may send many notes to the same user.
///
/// Non-internal notes are also delivered as a real-time push notification
/// so the user sees them immediately in their notification feed.
/// Internal notes are stored for admin eyes only and are never pushed.
///
/// Matches the "Send Note / Message" modal on the Platform Admin UI.
/// </summary>
public class AdminNote : BaseEntity
{
    /// <summary>The admin who sent the note (FK → Users).</summary>
    public Guid SentByAdminId { get; set; }

    /// <summary>The user who receives the note (FK → Users).</summary>
    public Guid RecipientUserId { get; set; }

    /// <summary>
    /// Category / severity pill from the UI.
    /// Values: Notice | Warning | Urgent | Internal
    /// </summary>
    public string Category { get; set; } = "Notice";

    /// <summary>
    /// Short label from the Quick Templates strip, or null if the admin
    /// typed a custom subject.
    /// e.g. "DocumentVerificationRequest", "FoodBagSurplusAlert",
    ///      "AccountLoyaltyBonus", "InternalModeration"
    /// </summary>
    public string? Template { get; set; }

    /// <summary>Note Title / Subject field.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Note body — the official message text.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// When true the note is stored for admin records only and is NOT
    /// delivered to the user as a notification.
    /// </summary>
    public bool IsInternal { get; set; }
}
