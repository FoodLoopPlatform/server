using FoodLoop.Domain.Common;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Domain.Entities;

/// <summary>
/// One uploaded verification document for a Organization.
/// </summary>
public class OrganizationVerification : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public UploadDocumentType VerificationType { get; set; }
    public string DocumentUrl { get; set; } = string.Empty;

    public VerificationStatus Status { get; set; } = VerificationStatus.Pending;
    public string? ReviewNote { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}

