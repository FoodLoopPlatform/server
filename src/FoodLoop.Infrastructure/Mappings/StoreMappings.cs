using FoodLoop.Application.DTOs.Stores;
using FoodLoop.Domain.Entities;

namespace FoodLoop.Infrastructure.Mappings;

internal static class StoreMappings
{
    public static StoreDto ToDto(this Store store) => new()
    {
        Id = store.Id,
        Name = store.Name,
        BusinessCategory = store.BusinessCategory,
        Governorate = store.Governorate,
        City = store.City,
        Neighborhood = store.Neighborhood,
        Street = store.Street,
        Latitude = store.Latitude,
        Longitude = store.Longitude,
        VerificationStatus = store.VerificationStatus.ToString(),
        Documents = store.Verifications.Select(v => new StoreDocumentDto
        {
            Id = v.Id,
            VerificationType = v.VerificationType,
            DocumentUrl = v.DocumentUrl,
            Status = v.Status.ToString(),
        }).ToArray(),
    };
}
