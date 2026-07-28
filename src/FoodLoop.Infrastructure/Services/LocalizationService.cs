using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Infrastructure.Resources;
using Microsoft.Extensions.Localization;

namespace FoodLoop.Infrastructure.Services;

/// <summary>
/// Wraps IStringLocalizer behind the ILocalizationService abstraction so Application-layer
/// handlers stay free of Microsoft.Extensions.Localization references.
///
/// Resource lookup:
///   IStringLocalizerFactory.Create(baseName, location) explicitly names the Infrastructure
///   assembly so the runtime finds Resources/FoodLoop.Infrastructure.Resources.Messages.{culture}.resx
///   regardless of which assembly called AddLocalization().
/// </summary>
public class LocalizationService : ILocalizationService
{
    private readonly IStringLocalizer _localizer;

    public LocalizationService(IStringLocalizerFactory factory)
    {
        // baseName  = fully-qualified type name used to locate the .resx file
        // location  = assembly name that owns the resource
        _localizer = factory.Create(
            baseName: typeof(Messages).FullName!,
            location: typeof(Messages).Assembly.GetName().Name!);
    }

    public string this[string key] => _localizer[key].Value;

    public string this[string key, params object[] args] => _localizer[key, args].Value;
}
