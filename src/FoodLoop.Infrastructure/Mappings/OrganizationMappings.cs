using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Domain.Entities;

namespace FoodLoop.Infrastructure.Mappings;

internal static class OrganizationMappings
{
    public static OrganizationDto ToDto(this Organization organization) => new()
    {
        Id = organization.Id,
        Name = organization.Name,
        NameAr = organization.NameAr,
        Description = organization.Description,
        DescriptionAr = organization.DescriptionAr,
        Logo = organization.Logo,
        Phone = organization.Phone,
        Email = organization.Email,
        BusinessCategory = organization.BusinessCategory,
        Governorate = organization.Governorate,
        City = organization.City,
        Neighborhood = organization.Neighborhood,
        Street = organization.Street,
        BuildingNo = organization.BuildingNo,
        Latitude = organization.Latitude,
        Longitude = organization.Longitude,
        VerificationStatus = organization.VerificationStatus.ToString(),
        AdminNote = organization.AdminNote,
        OpeningHours = organization.OpeningHours,
        Documents = organization.Verifications.Select(v => new OrganizationDocumentDto
        {
            Id = v.Id,
            VerificationType = v.VerificationType.ToString(),
            DocumentUrl = v.DocumentUrl,
            Status = v.Status.ToString(),
            ReviewNote = v.ReviewNote,
        }).ToArray(),
    };
}


