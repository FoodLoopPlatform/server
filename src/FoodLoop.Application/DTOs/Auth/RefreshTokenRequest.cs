using System.ComponentModel.DataAnnotations;

namespace FoodLoop.Application.DTOs.Auth;

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
