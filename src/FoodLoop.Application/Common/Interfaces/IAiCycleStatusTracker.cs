using FoodLoop.Application.Common.DTOs.AI;

namespace FoodLoop.Application.Common.Interfaces;

public interface IAiCycleStatusTracker
{
    void InitializeCycle(string cycleName, int intervalMinutes);
    void RecordCycleStarted(string cycleName);
    void RecordCycleCompleted(string cycleName, int nextIntervalMinutes);
    void RecordCycleFailed(string cycleName, string errorMessage, int nextIntervalMinutes);
    AiCycleStatusDto GetCycleStatus(string cycleName);
    AiCyclesOverviewDto GetAllCyclesStatus();
}
