using System.ComponentModel.DataAnnotations;

namespace FoodLoop.Infrastructure.Options;

public class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";

    [Required]
    public string Url { get; set; } = string.Empty;
}
