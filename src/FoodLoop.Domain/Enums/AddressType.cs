namespace FoodLoop.Domain.Enums;

/// <summary>
/// Matches the "Address Label" radio group on the add_address UI screen (Home / Company).
/// Other is kept as a superset option for addresses that don't fit either preset.
/// </summary>
public enum AddressType
{
    Home = 0,
    Company = 1,
    Other = 2
}
