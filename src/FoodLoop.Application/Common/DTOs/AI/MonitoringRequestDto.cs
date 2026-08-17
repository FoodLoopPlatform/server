using System;

namespace FoodLoop.Application.Common.DTOs.AI;

public record MonitoringRequestDto(
    MonitoringProductDto Product,
    MonitoringInventoryDto Inventory,
    MonitoringDemandDto Demand,
    MonitoringExpiryDto Expiry,
    MonitoringLocationDto Location,
    MonitoringStorePolicyDto? StorePolicy,
    DateTimeOffset Timestamp
);

public record MonitoringProductDto(
    string Id,
    string Name,
    string Category
);

public record MonitoringInventoryDto(
    int Quantity,
    decimal OriginalPrice,
    decimal CurrentPrice,
    decimal PriceFloor
);

public record MonitoringDemandDto(
    double SalesVelocity,
    MonitoringHistoricalSalesDto HistoricalSales
);

public record MonitoringHistoricalSalesDto(
    double AverageDailySales
);

public record MonitoringExpiryDto(
    DateTimeOffset ExpiresAt, // DateTimeOffset, UTC
    double HoursRemaining
);

public record MonitoringLocationDto(
    double Latitude,
    double Longitude,
    string StoreId
);

public record MonitoringStorePolicyDto(
    string StoreId,
    string OperatingMode // Lowercase "assisted" or "autonomous" only. Handled during mapping/serialization.
);
