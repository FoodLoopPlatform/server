using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.DTOs.Organizations;

/// <summary>Returned by GET /organizations/me. Used both for the re-entrant onboarding wizard
/// (step 2/3) and, later, the merchant dashboard.</summary>
public class OrganizationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? Description { get; set; }
    public string? DescriptionAr { get; set; }
    public string? Logo { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public BusinessCategory? BusinessCategory { get; set; }

    public string? Governorate { get; set; }
    public string? City { get; set; }
    public string? Neighborhood { get; set; }
    public string? Street { get; set; }
    public string? BuildingNo { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string VerificationStatus { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
    public string? OpeningHours { get; set; }
    public IReadOnlyList<OrganizationDocumentDto> Documents { get; set; } = Array.Empty<OrganizationDocumentDto>();
}

public class OrganizationDocumentDto
{
    public Guid Id { get; set; }
    public string VerificationType { get; set; } = string.Empty;
    public string DocumentUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
}

