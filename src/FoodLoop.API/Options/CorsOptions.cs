namespace FoodLoop.API.Options;

/// <summary>
/// Bound from the "Cors" configuration section via the options pattern (see Program.cs).
/// </summary>
public class CorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}
