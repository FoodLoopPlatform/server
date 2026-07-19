using System.ComponentModel.DataAnnotations;

namespace FoodLoop.Application.DTOs.Users;

/// <summary>PATCH /users/me — all fields optional; only supplied fields are updated.</summary>
public class UpdateProfileRequest
{
    [MaxLength(150)]
    public string? Name { get; set; }

    public string? ProfileImage { get; set; }

    [MaxLength(10)]
    public string? PreferredLanguage { get; set; }
}
