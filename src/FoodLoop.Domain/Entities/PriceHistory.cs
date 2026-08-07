using FoodLoop.Domain.Common;
using System;

namespace FoodLoop.Domain.Entities;

/// <summary>
/// Immutable record of every price change on a product.
/// Written by UpdateProductCommandHandler whenever OriginalPrice or DiscountedPrice changes.
/// Supports GET /stores/me/products/{id}/price-history (price_history_audit screen).
/// </summary>
public class PriceHistory : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal OldOriginalPrice { get; set; }
    public decimal OldDiscountedPrice { get; set; }
    public decimal NewOriginalPrice { get; set; }
    public decimal NewDiscountedPrice { get; set; }

    /// <summary>Free-text reason: "manual edit", "smart discount applied", etc.</summary>
    public string? ChangeReason { get; set; }

    public Guid ChangedBy { get; set; }
}
