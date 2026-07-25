using FoodLoop.Application.DTOs.Users;
using FoodLoop.Infrastructure.Identity;

namespace FoodLoop.Infrastructure.Mappings;

/// <summary>
/// Shared by every Auth/Users handler that needs to turn an ApplicationUser (plus its
/// Identity roles) into the DTO shape the API returns.
/// </summary>
internal static class UserMappings
{
    public static UserDto ToDto(this ApplicationUser user, IList<string> roles) => new()
    {
        Id = user.Id,
        FullName = user.FullName,
        Email = user.Email!,
        PhoneNumber = user.PhoneNumber,
        ProfileImage = user.ProfileImage,
        Language = user.Language,
        Status = user.Status.ToString(),
        OrderUpdatesEnabled = user.OrderUpdatesEnabled,
        MarketingNotificationsEnabled = user.MarketingNotificationsEnabled,
        Roles = roles.ToArray(),
        CreatedAt = user.CreatedAt,
    };
}
