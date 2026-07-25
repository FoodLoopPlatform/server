namespace FoodLoop.Domain.Enums;

/// <summary>
/// Business classification captured at signup (create_account_account_type_selection /
/// business_signup_step_1 UI screens offer "Merchant" and "Charity" as account types).
/// This mirrors the user's AppRole (Merchant vs Charity) onto the Store itself so
/// donation-related features (V2) can key off StoreType without re-deriving it from the owner's role.
/// </summary>
public enum StoreType
{
    Standard = 0,
    Charity = 1
}
