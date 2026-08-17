using System;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.Common.Extensions;

public static class AiOperatingModeExtensions
{
    public static string ToApiOperatingMode(this AiOperatingMode mode)
    {
        return mode switch
        {
            AiOperatingMode.Assisted => "assisted",
            AiOperatingMode.Autonomous => "autonomous",
            AiOperatingMode.Manual => throw new InvalidOperationException("Organizations in Manual operating mode are forbidden from invoking the AI service."),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), $"Unknown operating mode: {mode}")
        };
    }
}
