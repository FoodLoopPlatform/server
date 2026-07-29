using FoodLoop.Domain.Common;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Domain.Entities;

/// <summary>
/// One uploaded verification document for a Store.
/// </summary>
public class StoreVerification : BaseEntity
{
    public Guid StoreId { get; set; }
    public Store? Store { get; set; }

    public UploadDocumentType VerificationType { get; set; }
    public string DocumentUrl { get; set; } = string.Empty;

    public VerificationStatus Status { get; set; } = VerificationStatus.Pending;
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}
