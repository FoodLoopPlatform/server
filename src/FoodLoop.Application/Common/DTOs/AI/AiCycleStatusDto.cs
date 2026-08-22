using System;

namespace FoodLoop.Application.Common.DTOs.AI;

public record AiCycleStatusDto(
    string CycleName,
    bool IsRunning,
    DateTimeOffset? LastRunStartedAt,
    DateTimeOffset? LastRunCompletedAt,
    string Status,
    string? LastError,
    DateTimeOffset? NextRunExpectedAt,
    int IntervalMinutes
);

public record AiCyclesOverviewDto(
    AiCycleStatusDto MonitoringScanner,
    AiCycleStatusDto PricingBatch,
    AiCycleStatusDto HistoricalIngestion,
    DateTimeOffset? NextUpcomingCycleAt
);

public record StoreAiScheduleDto(
    DateTimeOffset? NextPricingBatchAt,
    DateTimeOffset? NextMonitoringScanAt,
    int PricingIntervalMinutes,
    bool IsPricingBatchRunning,
    string AutomationMode
);
