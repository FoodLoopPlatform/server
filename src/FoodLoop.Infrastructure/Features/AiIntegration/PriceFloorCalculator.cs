using FoodLoop.Domain.Enums;

namespace FoodLoop.Infrastructure.Features.AiIntegration;

public static class PriceFloorCalculator
{
    public static decimal Calculate(decimal originalPrice, PriceFloorPolicy? policy)
    {
        if (policy == PriceFloorPolicy.Fixed30Percent)
        {
            return originalPrice * 0.30m;
        }

        if (policy == PriceFloorPolicy.Fixed50Percent)
        {
            return originalPrice * 0.50m;
        }

        // DynamicAi or null/unrecognized fallback to 90% (intentional fallback to allow tight floor validations)
        return originalPrice * 0.90m;
    }
}
