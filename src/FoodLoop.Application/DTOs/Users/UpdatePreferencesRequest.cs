namespace FoodLoop.Application.DTOs.Users;

/// <summary>PATCH /users/me/preferences — matches the two toggles on profile_settings
/// ("Order Updates" / "Latest Offers").</summary>
public class UpdatePreferencesRequest
{
    public bool? OrderUpdatesEnabled { get; set; }
    public bool? MarketingNotificationsEnabled { get; set; }
    public string? PreferredLanguage { get; set; }
}
