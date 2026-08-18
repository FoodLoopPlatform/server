using FluentAssertions;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.AiIntegration;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.AiIntegration;

public class PriceFloorCalculatorTests
{
    [Fact]
    public void Calculate_should_return_30_percent_when_policy_is_Fixed30Percent()
    {
        // Arrange
        decimal originalPrice = 100.00m;
        var policy = PriceFloorPolicy.Fixed30Percent;

        // Act
        decimal result = PriceFloorCalculator.Calculate(originalPrice, policy);

        // Assert
        result.Should().Be(30.00m);
    }

    [Fact]
    public void Calculate_should_return_50_percent_when_policy_is_Fixed50Percent()
    {
        // Arrange
        decimal originalPrice = 100.00m;
        var policy = PriceFloorPolicy.Fixed50Percent;

        // Act
        decimal result = PriceFloorCalculator.Calculate(originalPrice, policy);

        // Assert
        result.Should().Be(50.00m);
    }

    [Fact]
    public void Calculate_should_return_90_percent_when_policy_is_DynamicAi()
    {
        // Arrange
        decimal originalPrice = 100.00m;
        var policy = PriceFloorPolicy.DynamicAi;

        // Act
        decimal result = PriceFloorCalculator.Calculate(originalPrice, policy);

        // Assert
        result.Should().Be(90.00m);
    }

    [Fact]
    public void Calculate_should_fallback_to_90_percent_when_policy_is_null()
    {
        // Arrange
        decimal originalPrice = 100.00m;
        PriceFloorPolicy? policy = null;

        // Act
        decimal result = PriceFloorCalculator.Calculate(originalPrice, policy);

        // Assert
        result.Should().Be(90.00m);
    }

    [Fact]
    public void Calculate_should_fallback_to_90_percent_when_policy_is_unrecognized()
    {
        // Arrange
        decimal originalPrice = 100.00m;
        PriceFloorPolicy policy = (PriceFloorPolicy)999; // Unrecognized/undefined enum value

        // Act
        decimal result = PriceFloorCalculator.Calculate(originalPrice, policy);

        // Assert
        result.Should().Be(90.00m);
    }

    [Fact]
    public void Calculate_with_extremely_small_original_price_preserves_precision()
    {
        // Scenario: micro-pricing (0.01m) — floor should scale proportionally.
        // Finding: confirms no precision loss at small decimal values.
        PriceFloorCalculator.Calculate(0.01m, PriceFloorPolicy.DynamicAi).Should().Be(0.009m);
    }

    [Fact]
    public void Calculate_with_large_original_price_does_not_overflow()
    {
        // Scenario: very large list price (999999.99m).
        // Finding: confirms decimal math handles large values.
        PriceFloorCalculator.Calculate(999999.99m, PriceFloorPolicy.DynamicAi).Should().Be(899999.991m);
    }
}
