using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Infrastructure.Resources;
using Microsoft.Extensions.Localization;

namespace FoodLoop.Infrastructure.Services;

/// <summary>
/// Wraps IStringLocalizer&lt;Messages&gt; behind the ILocalizationService abstraction so
/// Application-layer handlers stay free of Microsoft.Extensions.Localization references.
/// The active culture is set per-request by UseRequestLocalization via the
/// Accept-Language header.
/// </summary>
public class LocalizationService : ILocalizationService
{
    private readonly IStringLocalizer<Messages> _localizer;

    public LocalizationService(IStringLocalizer<Messages> localizer)
    {
        _localizer = localizer;
    }

    public string this[string key] => _localizer[key].Value;

    public string this[string key, params object[] args] => _localizer[key, args].Value;
}
