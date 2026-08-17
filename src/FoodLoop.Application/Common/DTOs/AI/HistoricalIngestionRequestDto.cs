using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Common.DTOs.AI;

public record HistoricalPricingEventDto(
    string EventId,
    string StoreId,
    string ProductId,
    string Category,
    DateTimeOffset RecordedAt,
    int Quantity,
    decimal CurrentPrice,
    decimal OriginalPrice,
    decimal PriceFloor,
    double SalesVelocity,
    double HistoricalAverageDailySales,
    double HoursRemaining,
    double DiscountPercentage,
    int UnitsSoldAfterDiscount,
    double SellThroughRate,
    string Outcome
);

public record HistoricalIngestionRequestDto(
    IReadOnlyList<HistoricalPricingEventDto> Events
);

public record HistoricalIngestionResponseDto(
    int AcceptedCount,
    int UpsertedCount,
    int FailedCount,
    IReadOnlyList<string> DocumentIds
);
