namespace FoodLoop.Domain.Enums;

/// <summary>
/// Canonical role names used for RBAC. These are seeded as ASP.NET Core Identity
/// roles at startup (see Infrastructure/Identity/IdentitySeeder.cs) and referenced
/// via [Authorize(Roles = ...)] rather than a hard enum on the user, since Identity
/// roles are string-based.
/// </summary>
public static class AppRole
{
    public const string Consumer = "Consumer";
    public const string Merchant = "Merchant";
    public const string Courier = "Courier";
    public const string Administrator = "Administrator";

    public static readonly string[] All = { Consumer, Merchant, Courier, Administrator };
}
