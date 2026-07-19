using System.ComponentModel.DataAnnotations;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.DTOs.Users;

/// <summary>Matches the add_address UI screen exactly.</summary>
public class CreateAddressRequest
{
    public AddressType AddressType { get; set; } = AddressType.Home;

    [Required, MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string District { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Street { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? BuildingNo { get; set; }

    [MaxLength(20)]
    public string? Floor { get; set; }

    [MaxLength(20)]
    public string? ApartmentNo { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }

    public bool IsDefault { get; set; }
}
