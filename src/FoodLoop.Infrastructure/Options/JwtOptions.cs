using System.ComponentModel.DataAnnotations;

namespace FoodLoop.Infrastructure.Options;

/// <summary>
/// Bound from the "Jwt" configuration section via the options pattern
/// (see InfrastructureServiceRegistration.AddInfrastructure).
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required]
    [MinLength(32, ErrorMessage = "Jwt:Secret must be at least 32 characters long.")]
    public string Secret { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int AccessTokenExpirationMinutes { get; set; } = 15;

    [Range(1, int.MaxValue)]
    public int RefreshTokenExpirationDays { get; set; } = 30;
}
