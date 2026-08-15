namespace FoodLoop.Domain.Enums;

/// <summary>
/// Automation mode for merchant store pricing and discounts:
/// Manual: AI suggests discounts, merchant applies manually.
/// Assisted: AI suggests daily pricing / near-expiry discounts with 1-click approval.
/// Autonomous: AI updates prices dynamically within safety limits.
/// </summary>
public enum AutomationMode
{
    Manual = 0,
    Assisted = 1,
    Autonomous = 2
}
