using System.ComponentModel.DataAnnotations;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.DTOs.Users;

public class UpdateAddressRequest
{
    public AddressType? AddressType { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? District { get; set; }

    [MaxLength(200)]
    public string? Street { get; set; }

    [MaxLength(20)]
    public string? BuildingNo { get; set; }

    [MaxLength(20)]
    public string? Floor { get; set; }

    [MaxLength(20)]
    public string? ApartmentNo { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }

    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }

    public bool? IsDefault { get; set; }
}
