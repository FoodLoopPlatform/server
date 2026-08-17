using System.ComponentModel.DataAnnotations;

namespace FoodLoop.Infrastructure.Options;

public class MonitoringScannerOptions
{
    public const string SectionName = "MonitoringScanner";

    [Range(1, 1440, ErrorMessage = "IntervalMinutes must be between 1 and 1440 (24 hours).")]
    public int IntervalMinutes { get; set; } = 60;

    [Range(1, 365, ErrorMessage = "ExpirationThresholdDays must be between 1 and 365 days.")]
    public int ExpirationThresholdDays { get; set; } = 3;

    [Range(0.01, 10.0, ErrorMessage = "VelocityThresholdMultiplier must be between 0.01 and 10.0.")]
    public double VelocityThresholdMultiplier { get; set; } = 0.8;
}
