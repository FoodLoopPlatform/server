namespace FoodLoop.Application.Common.Interfaces;

/// <summary>
/// Thin wrapper around IStringLocalizer so Application-layer handlers can retrieve
/// translated strings without taking a direct dependency on Microsoft.Extensions.Localization.
/// The active culture is resolved from the incoming request's Accept-Language header by
/// UseRequestLocalization (wired in Program.cs).
/// </summary>
public interface ILocalizationService
{
    /// <summary>Returns the localized string for <paramref name="key"/> in the current
    /// request culture. Falls back to English when no translation is found.</summary>
    string this[string key] { get; }

    /// <summary>Returns the localized string formatted with <paramref name="args"/>.</summary>
    string this[string key, params object[] args] { get; }
}
