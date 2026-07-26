using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.DTOs.Stores;

/// <summary>Returned by GET /stores/me. Used both for the re-entrant onboarding wizard
/// (step 2/3) and, later, the merchant dashboard.</summary>
public class StoreDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public BusinessCategory? BusinessCategory { get; set; }

    public string? Governorate { get; set; }
    public string? City { get; set; }
    public string? Neighborhood { get; set; }
    public string? Street { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string VerificationStatus { get; set; } = string.Empty;
    public IReadOnlyList<StoreDocumentDto> Documents { get; set; } = Array.Empty<StoreDocumentDto>();
}

public class StoreDocumentDto
{
    public Guid Id { get; set; }
    public string VerificationType { get; set; } = string.Empty;
    public string DocumentUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
