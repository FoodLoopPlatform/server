using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.DTOs.Admin;

/// <summary>Organization representation returned by admin endpoints â€” includes owner info and all documents.</summary>
public class AdminOrganizationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public BusinessCategory? BusinessCategory { get; set; }
    public string? Logo { get; set; }
    public string? CoverPhoto { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public string? Governorate { get; set; }
    public string? City { get; set; }
    public string? Neighborhood { get; set; }
    public string? Street { get; set; }
    public string? BuildingNo { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string VerificationStatus { get; set; } = string.Empty;
    public string? AdminNote { get; set; }

    // Owner info â€” shown to admin for context
    public Guid OwnerId { get; set; }
    public string OwnerEmail { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerPhone { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public IReadOnlyList<AdminOrganizationDocumentDto> Documents { get; set; } = Array.Empty<AdminOrganizationDocumentDto>();
}

public class AdminOrganizationDocumentDto
{
    public Guid Id { get; set; }
    public string VerificationType { get; set; } = string.Empty;
    public string DocumentUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}

