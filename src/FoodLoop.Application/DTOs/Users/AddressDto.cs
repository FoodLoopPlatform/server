using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.DTOs.Users;

public class AddressDto
{
    public Guid Id { get; set; }
    public AddressType AddressType { get; set; }
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
