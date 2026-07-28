using System.ComponentModel.DataAnnotations;

namespace FoodLoop.Application.DTOs.Users;

public class UpdateUserRequest
{
    [MaxLength(150)]
    public string? FullName { get; set; }

    [EmailAddress, MaxLength(256)]
    public string? Email { get; set; }

    [Phone, MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [RegularExpression("^(en|ar)$", ErrorMessage = "Language must be 'en' or 'ar'.")]
    public string? Language { get; set; }

    public string? Status { get; set; }
    public string? Role { get; set; }
}
