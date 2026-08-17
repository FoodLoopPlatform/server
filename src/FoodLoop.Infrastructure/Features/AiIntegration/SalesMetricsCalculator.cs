using System;
using System.Collections.Generic;
using System.Linq;

namespace FoodLoop.Infrastructure.Features.AiIntegration;

public static class SalesMetricsCalculator
{
    public class Metrics
    {
        public double SalesVelocity { get; set; }
        public double HistoricalAverageDailySales { get; set; }
    }

    public class OrderItemSummary
    {
        public int Quantity { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public static Metrics Calculate(
        IEnumerable<OrderItemSummary> orderItems,
        DateTimeOffset createdAt,
        DateTimeOffset referenceTime)
    {
        var ageInDays = (referenceTime - createdAt).TotalDays;
        if (ageInDays < 1.0) ageInDays = 1.0;

        var historicalDays = Math.Min(30.0, ageInDays);
        var qtyLast30Days = orderItems.Where(oi => oi.CreatedAt >= referenceTime.AddDays(-30) && oi.CreatedAt <= referenceTime).Sum(oi => oi.Quantity);
        var historicalAvg = qtyLast30Days / historicalDays;

        var velocityDays = Math.Min(7.0, ageInDays);
        var qtyLast7Days = orderItems.Where(oi => oi.CreatedAt >= referenceTime.AddDays(-7) && oi.CreatedAt <= referenceTime).Sum(oi => oi.Quantity);
        var salesVelocity = qtyLast7Days / velocityDays;

        return new Metrics
        {
            SalesVelocity = salesVelocity,
            HistoricalAverageDailySales = historicalAvg
        };
    }
}
