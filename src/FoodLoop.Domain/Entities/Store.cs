using FoodLoop.Domain.Common;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Domain.Entities;

/// <summary>Represents a merchant's business. Full CRUD ships in Sprint 2; the model is
/// established now so migrations and relations are stable from the start.
///
/// A draft Store (Name + StoreType + BusinessCategory only, no location yet) is created by
/// AuthService.RegisterAsync when the signup's AccountType is StoreOwner/Charity — matching
/// business_signup_step_1. Location and documents are filled in afterwards via
/// StoreOnboardingController, matching business_verification_location (step 2) and
/// verification_pending_step_3 (step 3).</summary>
public class Store : BaseEntity, ISoftDelete
{
    public Guid OwnerId { get; set; } // FK -> ApplicationUser.Id (Merchant)

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Logo { get; set; }

    public StoreType StoreType { get; set; } = StoreType.Standard;
    public BusinessCategory? BusinessCategory { get; set; }

    // Structured location, matching the business_verification_location UI screen.
    // Left null until step 2 of the onboarding wizard is completed.
    public string? Governorate { get; set; }
    public string? City { get; set; }
    public string? Neighborhood { get; set; }
    public string? Street { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string? OpeningHours { get; set; } // JSON-encoded schedule

    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Unverified;
    public double AverageRating { get; set; }

    public ICollection<ProductListing> ProductListings { get; set; } = new List<ProductListing>();
    public ICollection<StoreVerification> Verifications { get; set; } = new List<StoreVerification>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();

    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public bool IsDeleted { get; set; }
}
