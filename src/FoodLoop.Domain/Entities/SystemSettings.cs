using FoodLoop.Domain.Common;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Domain.Entities;

/// <summary>
/// Singleton row that stores platform-wide operational configuration.
/// There is always exactly one row (Id = SystemSettings.SingletonId).
/// Managed via PATCH /admin/system-settings (admin only).
/// </summary>
public class SystemSettings : BaseEntity
{
    /// <summary>Fixed GUID so there is always exactly one row in the table.</summary>
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000001");

    // ── Pricing ───────────────────────────────────────────────────────────

    /// <summary>
    /// Platform-wide hard ceiling on AI auto-discount per repricing cycle.
    /// Capped at 15 % as mandated by spec. Each store's per-store setting cannot exceed this.
    /// </summary>
    public int MaxDiscountPerCyclePercent { get; set; } = 10;

    /// <summary>Default price floor rule applied when a merchant has not set their own.</summary>
    public PriceFloorPolicy DefaultPriceFloorPolicy { get; set; } = PriceFloorPolicy.DynamicAi;

    // ── Automation ────────────────────────────────────────────────────────

    /// <summary>
    /// AutomationMode pre-assigned to a newly registered store at signup.
    /// The merchant can change it after onboarding via their AI Settings screen.
    /// </summary>
    public AutomationMode NewBusinessDefaultAutomationMode { get; set; } = AutomationMode.Assisted;

    // ── Onboarding ────────────────────────────────────────────────────────

    /// <summary>
    /// When true, newly registered stores bypass the manual admin review queue and
    /// are set to Verified automatically. Should only be enabled in controlled environments.
    /// </summary>
    public bool AutoVerifyPartnerStores { get; set; } = false;

    // ── Features ──────────────────────────────────────────────────────────

    /// <summary>
    /// Allows partner stores to upload and update their inventory catalogues via
    /// Excel/CSV bulk upload. When disabled, the bulk-upload endpoint returns 403.
    /// </summary>
    public bool BulkProductUploadEnabled { get; set; } = true;

    // ── Financials ────────────────────────────────────────────────────────

    /// <summary>Platform commission percentage deducted from each completed order.</summary>
    public int PlatformCommissionPercent { get; set; } = 10;

    // ── Rate Limiting ─────────────────────────────────────────────────────

    /// <summary>Maximum API requests per minute per client (used by rate-limit middleware).</summary>
    public int ApiRequestRateLimitPerMinute { get; set; } = 120;

    /// <summary>The threshold of expired product reports before a store is automatically deactivated.</summary>
    public int MaxExpiredReportsBeforeDeactivation { get; set; } = 3;
}
