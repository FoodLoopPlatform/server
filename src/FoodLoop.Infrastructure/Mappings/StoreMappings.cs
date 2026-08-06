using FoodLoop.Application.DTOs.Stores;
using FoodLoop.Domain.Entities;

namespace FoodLoop.Infrastructure.Mappings;

internal static class StoreMappings
{
    public static StoreDto ToDto(this Store store) => new()
    {
        Id = store.Id,
        Name = store.Name,
        NameAr = store.NameAr,
        Description = store.Description,
        DescriptionAr = store.DescriptionAr,
        Logo = store.Logo,
        Phone = store.Phone,
        Email = store.Email,
        BusinessCategory = store.BusinessCategory,
        Governorate = store.Governorate,
        City = store.City,
        Neighborhood = store.Neighborhood,
        Street = store.Street,
        BuildingNo = store.BuildingNo,
        Latitude = store.Latitude,
        Longitude = store.Longitude,
        VerificationStatus = store.VerificationStatus.ToString(),
        AdminNote = store.AdminNote,
        OpeningHours = store.OpeningHours,
        Documents = store.Verifications.Select(v => new StoreDocumentDto
        {
            Id = v.Id,
            VerificationType = v.VerificationType.ToString(),
            DocumentUrl = v.DocumentUrl,
            Status = v.Status.ToString(),
            ReviewNote = v.ReviewNote,
        }).ToArray(),
    };
}
