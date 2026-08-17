using System;
using System.Collections.Generic;
using FluentAssertions;
using FoodLoop.Infrastructure.Features.AiIntegration;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.AiIntegration;

public class SalesMetricsCalculatorTests
{
    private static readonly DateTimeOffset ReferenceTime = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Calculate_with_no_orders_should_return_zero_velocity_and_zero_historical_avg()
    {
        // Scenario: store with zero historical orders — no divide-by-zero.
        // Finding: confirms existing behavior — both metrics are 0.
        var createdAt = ReferenceTime.AddDays(-30);

        var metrics = SalesMetricsCalculator.Calculate(Array.Empty<SalesMetricsCalculator.OrderItemSummary>(), createdAt, ReferenceTime);

        metrics.SalesVelocity.Should().Be(0);
        metrics.HistoricalAverageDailySales.Should().Be(0);
    }

    [Fact]
    public void Calculate_with_product_younger_than_one_day_should_clamp_age_to_one_day()
    {
        // Scenario: product created 6 hours ago with 2 units sold — age clamped to 1 day minimum.
        // Finding: confirms ageInDays floor of 1.0 prevents inflated velocity.
        var createdAt = ReferenceTime.AddHours(-6);
        var orders = new List<SalesMetricsCalculator.OrderItemSummary>
        {
            new() { Quantity = 2, CreatedAt = ReferenceTime.AddHours(-2) }
        };

        var metrics = SalesMetricsCalculator.Calculate(orders, createdAt, ReferenceTime);

        metrics.SalesVelocity.Should().Be(2.0 / 1.0);
        metrics.HistoricalAverageDailySales.Should().Be(2.0 / 1.0);
    }

    [Fact]
    public void Calculate_should_ignore_orders_outside_30_day_window()
    {
        // Scenario: old order beyond 30-day window should not affect historical average.
        // Finding: confirms window filtering works correctly.
        var createdAt = ReferenceTime.AddDays(-60);
        var orders = new List<SalesMetricsCalculator.OrderItemSummary>
        {
            new() { Quantity = 100, CreatedAt = ReferenceTime.AddDays(-45) },
            new() { Quantity = 3, CreatedAt = ReferenceTime.AddDays(-5) }
        };

        var metrics = SalesMetricsCalculator.Calculate(orders, createdAt, ReferenceTime);

        metrics.HistoricalAverageDailySales.Should().Be(3.0 / 30.0);
        metrics.SalesVelocity.Should().Be(3.0 / 7.0);
    }

    [Fact]
    public void Calculate_with_zero_historical_avg_should_not_be_usable_for_low_velocity_trigger()
    {
        // Scenario: zero orders → historicalAvg=0 → low-velocity check in scanner is skipped.
        // Finding: documents the guard condition used by RunMonitoringScanCommandHandler.
        var createdAt = ReferenceTime.AddDays(-30);
        var metrics = SalesMetricsCalculator.Calculate(Array.Empty<SalesMetricsCalculator.OrderItemSummary>(), createdAt, ReferenceTime);

        var isLowVelocity = metrics.HistoricalAverageDailySales > 0
            && metrics.SalesVelocity < metrics.HistoricalAverageDailySales * 0.8;

        isLowVelocity.Should().BeFalse();
    }
}
