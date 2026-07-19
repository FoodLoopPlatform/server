using System.ComponentModel.DataAnnotations;

namespace FoodLoop.Application.DTOs.Auth;

public class LogoutRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
