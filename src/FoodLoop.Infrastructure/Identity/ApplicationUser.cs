using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Identity;

/// <summary>
/// Extends ASP.NET Core Identity's user model with the profile fields from the
/// Database Design doc's User table (fullName, language, profileImage, status).
/// Authentication concerns (password hash, email confirmation, lockout, etc.)
/// are handled entirely by Identity.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public string? ProfileImage { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public decimal WalletBalance { get; set; } = 0.00m;

    // Matches the two toggles on the profile_settings UI screen ("Order Updates" / "Latest Offers").
    public bool OrderUpdatesEnabled { get; set; } = true;
    public bool MarketingNotificationsEnabled { get; set; } = false;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public ICollection<Address> Addresses { get; set; } = new List<Address>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
