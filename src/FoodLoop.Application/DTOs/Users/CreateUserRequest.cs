using System.ComponentModel.DataAnnotations;

namespace FoodLoop.Application.DTOs.Users;

public class CreateUserRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [Phone, MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public string Role { get; set; } = "Customer";

    public string Status { get; set; } = "Active";
}
