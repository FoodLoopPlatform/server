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
}
