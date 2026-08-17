using FoodLoop.Domain.Common;
using System;

namespace FoodLoop.Domain.Entities;

public class ProductPricingEpisode : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public string EventId { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
    public DateTimeOffset IngestedAt { get; set; }
    public string IngestionCorrelationId { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public double DiscountPercentage { get; set; }
    public double SellThroughRate { get; set; }
}
