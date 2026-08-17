using System;
using System.Threading;
using System.Threading.Tasks;
using FoodLoop.Application.Common.Interfaces.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FoodLoop.Infrastructure.Integrations.AiService;

public class AiServiceHealthCheck : IHealthCheck
{
    private readonly IAiServiceClient _aiClient;

    public AiServiceHealthCheck(IAiServiceClient aiClient)
    {
        _aiClient = aiClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var ready = await _aiClient.GetReadyAsync(cancellationToken);
            if (ready != null && ready.Status.Equals("ready", StringComparison.OrdinalIgnoreCase))
            {
                return HealthCheckResult.Healthy("AI Service is ready and operational.");
            }
            
            return HealthCheckResult.Degraded($"AI Service reported readiness status: {ready?.Status ?? "unknown"}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Degraded: AI Service Unavailable", ex);
        }
    }
}
