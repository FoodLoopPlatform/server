using FoodLoop.Application.DTOs.Users;
using FoodLoop.Domain.Entities;

namespace FoodLoop.Infrastructure.Mappings;

internal static class AddressMappings
{
    public static AddressDto ToDto(this Address address) => new()
    {
        Id = address.Id,
        AddressType = address.AddressType,
        City = address.City,
        District = address.District,
        Street = address.Street,
        BuildingNo = address.BuildingNo,
        Floor = address.Floor,
        ApartmentNo = address.ApartmentNo,
        Notes = address.Notes,
        Latitude = address.Latitude,
        Longitude = address.Longitude,
        IsDefault = address.IsDefault,
    };
}
