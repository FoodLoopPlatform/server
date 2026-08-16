using System.ComponentModel.DataAnnotations;

namespace FoodLoop.Infrastructure.Options;

public class PaymobOptions
{
    public const string SectionName = "Paymob";

    [Required]
    public string BaseUrl { get; set; } = "https://accept.paymob.com";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string IntegrationId { get; set; } = string.Empty;

    [Required]
    public string IframeId { get; set; } = string.Empty;

    [Required]
    public string PublicKey { get; set; } = string.Empty;

    [Required]
    public string HmacSecret { get; set; } = string.Empty;
}
