using System;

namespace FoodLoop.Application.Common.DTOs.AI;

public record AiPricingRecommendationDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal DiscountPercentage,
    string Reason,
    double Confidence,
    string ActionRequirement,
    string ActionReason,
    string Status,
    string CorrelationId,
    DateTimeOffset CreatedAt
);
