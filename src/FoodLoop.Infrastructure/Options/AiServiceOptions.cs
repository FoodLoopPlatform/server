using System.ComponentModel.DataAnnotations;

namespace FoodLoop.Infrastructure.Options;

public class AiServiceOptions
{
    public const string SectionName = "AiService";

    [Required(ErrorMessage = "AiService:BaseUrl is required.")]
    [Url(ErrorMessage = "AiService:BaseUrl must be a valid URL.")]
    public string BaseUrl { get; set; } = string.Empty;

    [Range(1, 120, ErrorMessage = "TimeoutSeconds must be between 1 and 120 seconds.")]
    public int TimeoutSeconds { get; set; } = 30;

    [Range(1, 1000, ErrorMessage = "MaxPricingBatchSize must be between 1 and 1000.")]
    public int MaxPricingBatchSize { get; set; } = 50;
}
