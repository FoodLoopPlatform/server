using System.ComponentModel.DataAnnotations;

namespace FoodLoop.Application.DTOs.Auth;

public class ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}
