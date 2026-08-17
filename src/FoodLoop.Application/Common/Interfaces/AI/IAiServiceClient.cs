using FoodLoop.Application.Common.DTOs.AI;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Application.Common.Interfaces.AI;

public interface IAiServiceClient
{
    Task<MonitoringResponseDto> AnalyzeMonitoringAsync(MonitoringRequestDto request, CancellationToken ct = default);
    Task<PricingBatchResponseDto> RecommendPricingAsync(PricingBatchRequestDto request, CancellationToken ct = default);
    Task<AiServiceHealthDto> GetHealthAsync(CancellationToken ct = default);
    Task<AiServiceReadyDto> GetReadyAsync(CancellationToken ct = default);
    Task<HistoricalIngestionResponseDto> IngestHistoricalPricingAsync(HistoricalIngestionRequestDto request, CancellationToken ct = default);
}
