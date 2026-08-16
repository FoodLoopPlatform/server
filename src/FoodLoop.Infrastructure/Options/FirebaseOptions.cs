namespace FoodLoop.Infrastructure.Options;

public class FirebaseOptions
{
    public const string SectionName = "Firebase";

    public bool Enabled { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public string ServiceAccountJson { get; set; } = string.Empty;
    public string ServiceAccountJsonPath { get; set; } = string.Empty;
}
