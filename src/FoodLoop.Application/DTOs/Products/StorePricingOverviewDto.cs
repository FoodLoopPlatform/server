using System;
using System.Collections.Generic;

namespace FoodLoop.Application.DTOs.Products;

public class StorePricingOverviewDto
{
    public PricingSummaryDto Summary { get; set; } = new();
    public List<ProductPricingDto> Products { get; set; } = new();
}

public class PricingSummaryDto
{
    /// <summary>Number of active (non-deleted) products included in the pricing metrics.</summary>
    public int TotalActiveProducts { get; set; }
    public decimal AverageDiscountPercentage { get; set; }
    public decimal MaxDiscountPercentage { get; set; }
    public decimal MinDiscountPercentage { get; set; }
    public decimal TotalValueAtOriginalPrice { get; set; }
    public decimal TotalValueAtDiscountedPrice { get; set; }
    public decimal TotalPotentialSavings { get; set; }
}

public class ProductPricingDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal OriginalPrice { get; set; }
    public decimal DiscountedPrice { get; set; }
    public decimal DiscountPercentage { get; set; }
    public int QuantityAvailable { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly ExpirationDate { get; set; }
}
