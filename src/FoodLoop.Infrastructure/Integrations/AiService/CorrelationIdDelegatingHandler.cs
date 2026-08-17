using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FoodLoop.Application.Common.Interfaces;

namespace FoodLoop.Infrastructure.Integrations.AiService;

public class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private const string CorrelationIdHeaderKey = "X-Correlation-ID";

    public CorrelationIdDelegatingHandler(ICorrelationIdAccessor correlationIdAccessor)
    {
        _correlationIdAccessor = correlationIdAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Propagate trace correlation identifier to external downstream request headers
        if (!request.Headers.Contains(CorrelationIdHeaderKey))
        {
            var correlationId = _correlationIdAccessor.GetCorrelationId();
            request.Headers.Add(CorrelationIdHeaderKey, correlationId);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
