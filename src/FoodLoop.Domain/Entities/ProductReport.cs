using FoodLoop.Domain.Common;
using System;

namespace FoodLoop.Domain.Entities;

/// <summary>
/// A user-submitted report against a product listing.
/// Supports POST /marketplace/products/{id}/report (report_an_issue screen).
/// </summary>
public class ProductReport : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public Guid ReportedBy { get; set; }

    /// <summary>e.g. "MisleadingInfo", "WrongExpiry", "Spam", "Other"</summary>
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }

    public bool IsResolved { get; set; }
    public string? AdminNote { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}
