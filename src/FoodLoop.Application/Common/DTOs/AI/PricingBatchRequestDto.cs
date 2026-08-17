using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Common.DTOs.AI;

public record PricingBatchRequestDto(
    string StoreId,
    PricingStorePolicyDto StorePolicy, // Same lowercase operating mode constraint
    IReadOnlyList<PricingProductRequestDto> Products
);

public record PricingStorePolicyDto(
    string StoreId,
    string OperatingMode
);

public record PricingProductRequestDto(
    string ProductId,
    string ProductName,
    string Category,
    PricingInventoryDto Inventory,
    PricingDemandDto Demand,
    PricingExpiryDto Expiry,
    PricingRiskAssessmentDto RiskAssessment
);

public record PricingInventoryDto(
    int Quantity,
    decimal OriginalPrice,
    decimal CurrentPrice,
    decimal PriceFloor
);

public record PricingDemandDto(
    double SalesVelocity,
    PricingHistoricalSalesDto HistoricalSales
);

public record PricingHistoricalSalesDto(
    double AverageDailySales
);

public record PricingExpiryDto(
    DateTimeOffset ExpiresAt,
    double HoursRemaining
);

public record PricingRiskAssessmentDto(
    string RiskLevel,
    string Reason,
    double Confidence
);
