namespace FoodLoop.Domain.Enums;

/// <summary>
/// Valid categories for user-submitted product reports.
/// </summary>
public enum ProductReportReason
{
    MisleadingInfo,
    WrongExpiry,
    Expired,
    Spam,
    Inappropriate,
    Other
}
