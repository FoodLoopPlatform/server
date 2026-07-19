using FoodLoop.Domain.Common;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Domain.Entities;

/// <summary>A saved address belonging to a user (GET/POST/PATCH/DELETE /users/me/addresses).
/// Fields match the add_address UI screen exactly: a Home/Company label, a City picker,
/// District/Street text fields, Building/Floor/Apartment numbers, free-text Notes, and a
/// map-pin lat/lng.</summary>
public class Address : BaseEntity
{
    public Guid UserId { get; set; }

    public AddressType AddressType { get; set; } = AddressType.Home;

    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string? BuildingNo { get; set; }
    public string? Floor { get; set; }
    public string? ApartmentNo { get; set; }
    public string? Notes { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public bool IsDefault { get; set; }
}
