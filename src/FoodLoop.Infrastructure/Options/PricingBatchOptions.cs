using System.ComponentModel.DataAnnotations;

namespace FoodLoop.Infrastructure.Options;

public class PricingBatchOptions
{
    public const string SectionName = "AiPricingBatch";

    [Range(1, 1440, ErrorMessage = "IntervalMinutes must be between 1 and 1440 (24 hours).")]
    public int IntervalMinutes { get; set; } = 60;
}
