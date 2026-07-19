namespace FoodLoop.Domain.Enums;

/// <summary>
/// The account type selected at signup (create_account_account_type_selection UI screen:
/// "User" / "Store Owner" / "Charity"). This is a registration-time input only — it is not
/// persisted verbatim on the user. It resolves to an AppRole (Consumer vs Merchant) plus,
/// for business accounts, a draft Store with the matching StoreType.
/// </summary>
public enum AccountType
{
    User = 0,
    StoreOwner = 1,
    Charity = 2
}
