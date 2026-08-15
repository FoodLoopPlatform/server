using System.ComponentModel.DataAnnotations;

namespace FoodLoop.Infrastructure.Options;

public sealed class AdminUserOptions
{
    public const string SectionName = "AdminUser";

    [Required]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    [Required]
    public string FullName { get; init; } = "System Administrator";
}