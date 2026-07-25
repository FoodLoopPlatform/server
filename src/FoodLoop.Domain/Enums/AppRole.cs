namespace FoodLoop.Domain.Enums;

/// <summary>
/// Canonical role names used for RBAC. These are seeded as ASP.NET Core Identity
/// roles at startup (see Infrastructure/Identity/IdentitySeeder.cs) and referenced
/// via [Authorize(Roles = ...)] rather than a hard enum on the user, since Identity
/// roles are string-based. This is the single source of truth for a user's account
/// type — there is no separate AccountType concept; Customer/Merchant/Charity are
/// chosen directly at signup, and Admin is granted only by an existing admin.
/// </summary>
public static class AppRole
{
    public const string Customer = "Customer";
    public const string Merchant = "Merchant";
    public const string Charity = "Charity";
    public const string Admin = "Admin";

    /// <summary>All roles a user may self-select at registration (excludes Admin).</summary>
    public static readonly string[] SelfRegisterable = { Customer, Merchant, Charity };

    public static readonly string[] All = { Customer, Merchant, Charity, Admin };
}
