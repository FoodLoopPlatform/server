using FoodLoop.Application.DTOs.Users;

namespace FoodLoop.Application.DTOs.Auth;

public class AuthResponse
{
    public UserDto User { get; set; } = default!;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset AccessTokenExpiresAt { get; set; }
}
