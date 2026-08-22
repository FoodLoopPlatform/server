using System;

namespace FoodLoop.Application.Common.DTOs.AI;

public record AiPricingRecommendationDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal OriginalPrice,
    decimal CurrentPrice,
    decimal RecommendedPrice,
    decimal DiscountPercentage,
    decimal DiscountAmount,
    int QuantityAvailable,
    DateOnly ExpirationDate,
    int DaysRemaining,
    string? ProductImageUrl,
    string? RiskLevel,
    string Reason,
    double Confidence,
    string ActionRequirement,
    string ActionReason,
    string Status,
    string CorrelationId,
    DateTimeOffset CreatedAt
);
