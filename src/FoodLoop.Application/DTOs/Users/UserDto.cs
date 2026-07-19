namespace FoodLoop.Application.DTOs.Users;

public class UserDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? ProfileImage { get; set; }
    public string Language { get; set; } = "en";
    public string Status { get; set; } = "Active";
    public bool OrderUpdatesEnabled { get; set; } = true;
    public bool MarketingNotificationsEnabled { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public DateTimeOffset CreatedAt { get; set; }
}
