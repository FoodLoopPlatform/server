using FoodLoop.Domain.Common;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Domain.Entities;

/// <summary>
/// One uploaded verification document for a Store, matching the three upload slots on the
/// document_upload_step_2 UI screen (see <see cref="DocumentTypes"/>). A Store is considered
/// fully submitted for review once all three types have at least one row.
/// </summary>
public class StoreVerification : BaseEntity
{
    public Guid StoreId { get; set; }
    public Store? Store { get; set; }

    public string VerificationType { get; set; } = string.Empty; // see DocumentTypes
    public string DocumentUrl { get; set; } = string.Empty;

    public VerificationStatus Status { get; set; } = VerificationStatus.Pending;
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}

/// <summary>Canonical VerificationType values, matching document_upload_step_2 exactly.</summary>
public static class DocumentTypes
{
    public const string CommercialRegistration = "CommercialRegistration";
    public const string TaxIdCertificate = "TaxIdCertificate";
    public const string StoreFacilityPhoto = "StoreFacilityPhoto";

    public static readonly string[] All = { CommercialRegistration, TaxIdCertificate, StoreFacilityPhoto };
}
