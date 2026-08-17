using FoodLoop.Domain.Common;
using System;

namespace FoodLoop.Domain.Entities;

public class ProductPricingEpisode : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public string EventId { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
    
    /// <summary>
    /// Timestamp when this episode was successfully ingested into the AI Service.
    /// Can be null if the episode correction reset it or if ingestion has not occurred.
    /// </summary>
    public DateTimeOffset? IngestedAt { get; set; }

    /// <summary>
    /// Propagation correlation ID for vector database auditing.
    /// Can be null if the episode correction reset it or if ingestion has not occurred.
    /// Important: Ingestion must reuse the identical EventId to support idempotent upsert corrections.
    /// </summary>
    public string? IngestionCorrelationId { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public double DiscountPercentage { get; set; }
    public double SellThroughRate { get; set; }
}
