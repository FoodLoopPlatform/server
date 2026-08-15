namespace FoodLoop.Domain.Enums;

/// <summary>
/// Platform-wide default price floor policy applied when a merchant has not
/// configured their own minimum price rule.
/// Matches the "Default Price Floor Policy" dropdown on the System Settings screen.
/// </summary>
public enum PriceFloorPolicy
{
    /// <summary>AI determines the floor dynamically based on cost signals.</summary>
    DynamicAi = 0,

    /// <summary>Floor is fixed at 30 % of the product's original price.</summary>
    Fixed30Percent = 1,

    /// <summary>Floor is fixed at 50 % of the product's original price.</summary>
    Fixed50Percent = 2
}
