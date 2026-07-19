namespace FoodLoop.Domain.Enums;

/// <summary>
/// Business classification captured at signup (create_account_account_type_selection /
/// business_signup_step_1 UI screens offer "Store Owner" and "Charity" as account types).
/// This is a Store attribute, not a separate RBAC role — both types authenticate as
/// AppRole.Merchant; Charity stores are simply flagged for donation-related features (V2).
/// </summary>
public enum StoreType
{
    Standard = 0,
    Charity = 1
}
