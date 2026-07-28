using System.ComponentModel.DataAnnotations;

namespace FoodLoop.Application.DTOs.Users;

/// <summary>PATCH /users/me — all fields optional; only supplied fields are updated.</summary>
public class UpdateProfileRequest
{
    [MaxLength(150)]
    public string? Name { get; set; }

    public string? ProfileImage { get; set; }

    [MaxLength(10)]
    [RegularExpression("^(en|ar)$", ErrorMessage = "Language must be 'en' or 'ar'.")]
    public string? PreferredLanguage { get; set; }
}
