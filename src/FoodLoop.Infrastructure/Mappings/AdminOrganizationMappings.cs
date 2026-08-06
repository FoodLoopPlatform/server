using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Identity;

namespace FoodLoop.Infrastructure.Mappings;

internal static class AdminOrganizationMappings
{
    public static AdminOrganizationDto ToAdminDto(this Organization organization, ApplicationUser owner) => new()
    {
        Id = organization.Id,
        Name = organization.Name,
        NameAr = organization.NameAr,
        Description = organization.Description,
        DescriptionAr = organization.DescriptionAr,
        BusinessCategory = organization.BusinessCategory,
        Logo = organization.Logo,
        Phone = organization.Phone,
        Email = organization.Email,
        Governorate = organization.Governorate,
        City = organization.City,
        Neighborhood = organization.Neighborhood,
        Street = organization.Street,
        BuildingNo = organization.BuildingNo,
        Latitude = organization.Latitude,
        Longitude = organization.Longitude,
        VerificationStatus = organization.VerificationStatus.ToString(),
        AdminNote = organization.AdminNote,
        OwnerId = organization.OwnerId,
        OwnerEmail = owner.Email ?? string.Empty,
        OwnerName = owner.FullName,
        OwnerPhone = owner.PhoneNumber,
        CreatedAt = organization.CreatedAt,
        UpdatedAt = organization.UpdatedAt,
        Documents = organization.Verifications.Select(v => new AdminOrganizationDocumentDto
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


