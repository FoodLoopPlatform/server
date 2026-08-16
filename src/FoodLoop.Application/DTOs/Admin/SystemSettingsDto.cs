namespace FoodLoop.Application.DTOs.Admin;

/// <summary>
/// Response and request shape for GET and POST /admin/system-settings.
/// Matches every field on the Platform Admin → System Settings screen.
/// </summary>
public class SystemSettingsDto
{
    /// <summary>
    /// Hard ceiling on AI auto-discount per repricing cycle (1–15 %).
    /// Per-store settings cannot exceed this platform-wide cap.
    /// </summary>
    public int MaxDiscountPerCyclePercent { get; set; }

    /// <summary>
    /// Default price floor rule when a merchant has not set their own.
    /// Values: "DynamicAi", "Fixed30Percent", "Fixed50Percent".
    /// </summary>
    public string DefaultPriceFloorPolicy { get; set; } = string.Empty;

    /// <summary>
    /// AutomationMode pre-assigned to a newly registered store at signup.
    /// Values: "Manual", "Assisted", "Autonomous".
    /// </summary>
    public string NewBusinessDefaultAutomationMode { get; set; } = string.Empty;

    /// <summary>When true, new stores bypass the manual admin verification queue.</summary>
    public bool AutoVerifyPartnerStores { get; set; }

    /// <summary>When true, partner stores may use the CSV/Excel bulk product upload endpoint.</summary>
    public bool BulkProductUploadEnabled { get; set; }

    /// <summary>Platform commission percentage deducted from each completed order (0–100).</summary>
    public int PlatformCommissionPercent { get; set; }

    /// <summary>Maximum API requests per minute per client.</summary>
    public int ApiRequestRateLimitPerMinute { get; set; }

    /// <summary>Timestamp of the last update.</summary>
    public DateTimeOffset? LastUpdatedAt { get; set; }
}
