using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.DTOs.Admin;

/// <summary>Store representation returned by admin endpoints — includes owner info and all documents.</summary>
public class AdminStoreDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? Description { get; set; }
    public string? DescriptionAr { get; set; }
    public BusinessCategory? BusinessCategory { get; set; }
    public string? Logo { get; set; }

    public string? Governorate { get; set; }
    public string? City { get; set; }
    public string? Neighborhood { get; set; }
    public string? Street { get; set; }
    public string? BuildingNo { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string VerificationStatus { get; set; } = string.Empty;

    // Owner info — shown to admin for context
    public Guid OwnerId { get; set; }
    public string OwnerEmail { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerPhone { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public IReadOnlyList<AdminStoreDocumentDto> Documents { get; set; } = Array.Empty<AdminStoreDocumentDto>();
}

public class AdminStoreDocumentDto
{
    public Guid Id { get; set; }
    public string VerificationType { get; set; } = string.Empty;
    public string DocumentUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? ReviewedAt { get; set; }
}
