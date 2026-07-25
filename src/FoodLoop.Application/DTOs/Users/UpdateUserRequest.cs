namespace FoodLoop.Application.DTOs.Users;

public class UpdateUserRequest
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Language { get; set; }
    public string? Status { get; set; }
    public string? Role { get; set; }
}
