using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Identity;

namespace FoodLoop.Infrastructure.Mappings;

internal static class AdminStoreMappings
{
    public static AdminStoreDto ToAdminDto(this Store store, ApplicationUser owner) => new()
    {
        Id = store.Id,
        Name = store.Name,
        NameAr = store.NameAr,
        Description = store.Description,
        DescriptionAr = store.DescriptionAr,
        BusinessCategory = store.BusinessCategory,
        Logo = store.Logo,
        Phone = store.Phone,
        Email = store.Email,
        Governorate = store.Governorate,
        City = store.City,
        Neighborhood = store.Neighborhood,
        Street = store.Street,
        BuildingNo = store.BuildingNo,
        Latitude = store.Latitude,
        Longitude = store.Longitude,
        VerificationStatus = store.VerificationStatus.ToString(),
        AdminNote = store.AdminNote,
        OwnerId = store.OwnerId,
        OwnerEmail = owner.Email ?? string.Empty,
        OwnerName = owner.FullName,
        OwnerPhone = owner.PhoneNumber,
        CreatedAt = store.CreatedAt,
        UpdatedAt = store.UpdatedAt,
        Documents = store.Verifications.Select(v => new AdminStoreDocumentDto
        {
            Id = v.Id,
            VerificationType = v.VerificationType.ToString(),
            DocumentUrl = v.DocumentUrl,
            Status = v.Status.ToString(),
            ReviewNote = v.ReviewNote,
            ReviewedAt = v.ReviewedAt,
        }).ToArray(),
    };
}
