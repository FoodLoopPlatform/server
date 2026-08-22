using System;
using System.Collections.Concurrent;
using System.Linq;
using FoodLoop.Application.Common.DTOs.AI;
using FoodLoop.Application.Common.Interfaces;

namespace FoodLoop.Infrastructure.Services;

public class AiCycleStatusTracker : IAiCycleStatusTracker
{
    private class CycleState
    {
        public string CycleName { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
        public DateTimeOffset? LastRunStartedAt { get; set; }
        public DateTimeOffset? LastRunCompletedAt { get; set; }
        public string Status { get; set; } = "Scheduled";
        public string? LastError { get; set; }
        public DateTimeOffset? NextRunExpectedAt { get; set; }
        public int IntervalMinutes { get; set; } = 60;
    }

    private readonly ConcurrentDictionary<string, CycleState> _cycles = new(StringComparer.OrdinalIgnoreCase);

    public AiCycleStatusTracker()
    {
        // Pre-seed default states so queries return valid data immediately on startup
        InitializeCycle("MonitoringScanner", 60);
        InitializeCycle("PricingBatch", 60);
        InitializeCycle("HistoricalIngestion", 60);
    }

    public void InitializeCycle(string cycleName, int intervalMinutes)
    {
        _cycles.AddOrUpdate(cycleName,
            _ => new CycleState
            {
                CycleName = cycleName,
                IntervalMinutes = intervalMinutes,
                Status = "Scheduled",
                NextRunExpectedAt = DateTimeOffset.UtcNow.AddMinutes(intervalMinutes)
            },
            (_, existing) =>
            {
                existing.IntervalMinutes = intervalMinutes;
                if (!existing.NextRunExpectedAt.HasValue)
                {
                    existing.NextRunExpectedAt = DateTimeOffset.UtcNow.AddMinutes(intervalMinutes);
                }
                return existing;
            });
    }

    public void RecordCycleStarted(string cycleName)
    {
        var state = _cycles.GetOrAdd(cycleName, name => new CycleState { CycleName = name });
        lock (state)
        {
            state.IsRunning = true;
            state.LastRunStartedAt = DateTimeOffset.UtcNow;
            state.Status = "Running";
        }
    }

    public void RecordCycleCompleted(string cycleName, int nextIntervalMinutes)
    {
        var state = _cycles.GetOrAdd(cycleName, name => new CycleState { CycleName = name });
        lock (state)
        {
            state.IsRunning = false;
            state.LastRunCompletedAt = DateTimeOffset.UtcNow;
            state.Status = "Success";
            state.LastError = null;
            state.IntervalMinutes = nextIntervalMinutes;
            state.NextRunExpectedAt = DateTimeOffset.UtcNow.AddMinutes(nextIntervalMinutes);
        }
    }

    public void RecordCycleFailed(string cycleName, string errorMessage, int nextIntervalMinutes)
    {
        var state = _cycles.GetOrAdd(cycleName, name => new CycleState { CycleName = name });
        lock (state)
        {
            state.IsRunning = false;
            state.LastRunCompletedAt = DateTimeOffset.UtcNow;
            state.Status = "Failed";
            state.LastError = errorMessage;
            state.IntervalMinutes = nextIntervalMinutes;
            state.NextRunExpectedAt = DateTimeOffset.UtcNow.AddMinutes(nextIntervalMinutes);
        }
    }

    public AiCycleStatusDto GetCycleStatus(string cycleName)
    {
        if (_cycles.TryGetValue(cycleName, out var state))
        {
            lock (state)
            {
                return new AiCycleStatusDto(
                    state.CycleName,
                    state.IsRunning,
                    state.LastRunStartedAt,
                    state.LastRunCompletedAt,
                    state.Status,
                    state.LastError,
                    state.NextRunExpectedAt,
                    state.IntervalMinutes
                );
            }
        }

        return new AiCycleStatusDto(
            cycleName,
            false,
            null,
            null,
            "Unknown",
            null,
            null,
            60
        );
    }

    public AiCyclesOverviewDto GetAllCyclesStatus()
    {
        var monitoring = GetCycleStatus("MonitoringScanner");
        var pricing = GetCycleStatus("PricingBatch");
        var historical = GetCycleStatus("HistoricalIngestion");

        var upcomingList = new[] { monitoring.NextRunExpectedAt, pricing.NextRunExpectedAt, historical.NextRunExpectedAt }
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .OrderBy(t => t)
            .ToList();

        DateTimeOffset? nextUpcoming = upcomingList.Count > 0 ? upcomingList[0] : null;

        return new AiCyclesOverviewDto(monitoring, pricing, historical, nextUpcoming);
    }
}
