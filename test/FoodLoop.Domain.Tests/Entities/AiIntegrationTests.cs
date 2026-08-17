using System;
using FluentAssertions;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using Xunit;

namespace FoodLoop.Domain.Tests.Entities;

public class AiIntegrationTests
{
    [Fact]
    public void AiRiskAssessment_valid_construction_should_succeed()
    {
        // Arrange & Act
        var productId = Guid.NewGuid();
        var assessment = new AiRiskAssessment(
            productId: productId,
            riskLevel: AiRiskLevel.HIGH,
            route: AiRoute.PRICING,
            reason: "Inventory item expiring soon",
            confidence: 0.85,
            requestedContext: "{\"weather\":\"hot\"}"
        );

        // Assert
        assessment.ProductId.Should().Be(productId);
        assessment.RiskLevel.Should().Be(AiRiskLevel.HIGH);
        assessment.Route.Should().Be(AiRoute.PRICING);
        assessment.Reason.Should().Be("Inventory item expiring soon");
        assessment.Confidence.Should().Be(0.85);
        assessment.RequestedContext.Should().Be("{\"weather\":\"hot\"}");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void AiRiskAssessment_confidence_in_bounds_should_succeed(double confidence)
    {
        // Act
        var assessment = new AiRiskAssessment { Confidence = confidence };

        // Assert
        assessment.Confidence.Should().Be(confidence);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void AiRiskAssessment_confidence_out_of_bounds_should_throw_ArgumentOutOfRangeException(double confidence)
    {
        // Arrange
        var assessment = new AiRiskAssessment();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => assessment.Confidence = confidence);
    }

    [Fact]
    public void AiPricingRecommendation_valid_construction_should_succeed()
    {
        // Arrange & Act
        var productId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var recommendation = new AiPricingRecommendation(
            productId: productId,
            organizationId: organizationId,
            discountPercentage: 12.5m,
            reason: "Apply minor discount",
            confidence: 0.9,
            actionRequirement: AiActionRequirement.APPROVAL_REQUIRED,
            actionReason: "Requires verification",
            correlationId: "CORR-1234",
            status: AiRecommendationStatus.Pending
        );

        // Assert
        recommendation.ProductId.Should().Be(productId);
        recommendation.OrganizationId.Should().Be(organizationId);
        recommendation.DiscountPercentage.Should().Be(12.5m);
        recommendation.Reason.Should().Be("Apply minor discount");
        recommendation.Confidence.Should().Be(0.9);
        recommendation.ActionRequirement.Should().Be(AiActionRequirement.APPROVAL_REQUIRED);
        recommendation.ActionReason.Should().Be("Requires verification");
        recommendation.CorrelationId.Should().Be("CORR-1234");
        recommendation.Status.Should().Be(AiRecommendationStatus.Pending);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(7.5)]
    [InlineData(15.0)]
    public void AiPricingRecommendation_discount_percentage_in_bounds_should_succeed(double discountPercentage)
    {
        // Act
        var recommendation = new AiPricingRecommendation { DiscountPercentage = (decimal)discountPercentage };

        // Assert
        recommendation.DiscountPercentage.Should().Be((decimal)discountPercentage);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(15.01)]
    public void AiPricingRecommendation_discount_percentage_out_of_bounds_should_throw_ArgumentOutOfRangeException(double discountPercentage)
    {
        // Arrange
        var recommendation = new AiPricingRecommendation();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => recommendation.DiscountPercentage = (decimal)discountPercentage);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void AiPricingRecommendation_confidence_in_bounds_should_succeed(double confidence)
    {
        // Act
        var recommendation = new AiPricingRecommendation { Confidence = confidence };

        // Assert
        recommendation.Confidence.Should().Be(confidence);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void AiPricingRecommendation_confidence_out_of_bounds_should_throw_ArgumentOutOfRangeException(double confidence)
    {
        // Arrange
        var recommendation = new AiPricingRecommendation();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => recommendation.Confidence = confidence);
    }

    [Fact]
    public void AiPricingRecommendation_default_status_should_be_pending()
    {
        // Act
        var recommendation = new AiPricingRecommendation();

        // Assert
        recommendation.Status.Should().Be(AiRecommendationStatus.Pending);
    }

    [Fact]
    public void Organization_should_construct_with_default_operating_mode_manual()
    {
        // Act
        var org = new Organization();

        // Assert
        org.AiOperatingMode.Should().Be(AiOperatingMode.Manual);
    }

    [Fact]
    public void Organization_legacy_fields_should_remain_untouched()
    {
        // Act
        var org = new Organization
        {
            AiAutoDiscountEnabled = true,
            AiAutoDiscountPercent = 25,
            AiAutoDiscountDaysBeforeExpiry = 4,
            AiAutoPricingEnabled = true
        };

        // Assert
        org.AiAutoDiscountEnabled.Should().BeTrue();
        org.AiAutoDiscountPercent.Should().Be(25);
        org.AiAutoDiscountDaysBeforeExpiry.Should().Be(4);
        org.AiAutoPricingEnabled.Should().BeTrue();
    }
}
