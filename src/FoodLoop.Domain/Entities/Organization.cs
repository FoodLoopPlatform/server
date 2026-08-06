using FoodLoop.Domain.Common;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Domain.Entities;

/// <summary>Represents a merchant's business. Full CRUD ships in Sprint 2; the model is
/// established now so migrations and relations are stable from the start.
///
/// A draft Organization (Name + BusinessCategory only, no location yet) is created by
/// RegisterCommandHandler when the signup's Role is Merchant â€” matching
/// business_signup_step_1. Location and documents are filled in afterwards via
/// OrganizationOnboardingController, matching business_verification_location (step 2) and
/// verification_pending_step_3 (step 3).</summary>
public class Organization : BaseEntity, ISoftDelete
{
    public Guid OwnerId { get; set; } // FK -> ApplicationUser.Id (Merchant)

    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? Description { get; set; }
    public string? DescriptionAr { get; set; }
    public string? Logo { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public BusinessCategory? BusinessCategory { get; set; }

    // Structured location, matching the business_verification_location UI screen.
    // Left null until step 2 of the onboarding wizard is completed.
    public string? Governorate { get; set; }
    public string? City { get; set; }
    public string? Neighborhood { get; set; }
    public string? Street { get; set; }
    public string? BuildingNo { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string? OpeningHours { get; set; } // JSON-encoded schedule

    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Unverified;
    public string? AdminNote { get; set; }
    public double AverageRating { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<OrganizationVerification> Verifications { get; set; } = new List<OrganizationVerification>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();

    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public bool IsDeleted { get; set; }
}


