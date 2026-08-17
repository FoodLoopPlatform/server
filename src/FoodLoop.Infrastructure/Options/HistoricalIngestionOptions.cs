using System.ComponentModel.DataAnnotations;

namespace FoodLoop.Infrastructure.Options;

public class HistoricalIngestionOptions
{
    public const string SectionName = "HistoricalIngestion";

    [Range(1, 1440, ErrorMessage = "IntervalMinutes must be between 1 and 1440 (24 hours).")]
    public int IntervalMinutes { get; set; } = 60;

    [Range(1, 1000, ErrorMessage = "BatchSize must be between 1 and 1000.")]
    public int BatchSize { get; set; } = 100;
}
