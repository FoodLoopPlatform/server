using System.ComponentModel.DataAnnotations;

namespace FoodLoop.Application.DTOs.Organizations;

/// <summary>PATCH /organizations/me/location â€” matches the business_verification_location UI screen
/// (step 2 of the business onboarding wizard).</summary>
public class UpdateStoreLocationRequest
{
    [Required, MaxLength(100)]
    public string Governorate { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Neighborhood { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Street { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? BuildingNo { get; set; }

    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }
}

