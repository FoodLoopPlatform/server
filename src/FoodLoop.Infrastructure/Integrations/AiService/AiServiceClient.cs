using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FoodLoop.Application.Common.DTOs.AI;
using FoodLoop.Application.Common.Exceptions.AI;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Interfaces.AI;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Registry;

namespace FoodLoop.Infrastructure.Integrations.AiService;

public class AiServiceClient : IAiServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;
    private readonly ILogger<AiServiceClient> _logger;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public AiServiceClient(
        HttpClient httpClient,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<AiServiceClient> logger,
        ICorrelationIdAccessor correlationIdAccessor)
    {
        _httpClient = httpClient;
        _pipelineProvider = pipelineProvider;
        _logger = logger;
        _correlationIdAccessor = correlationIdAccessor;

        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<MonitoringResponseDto> AnalyzeMonitoringAsync(MonitoringRequestDto request, CancellationToken ct = default)
    {
        _logger.LogInformation("Sending monitoring analysis request. CorrelationId: {CorrelationId}", _correlationIdAccessor.GetCorrelationId());

        var pipeline = _pipelineProvider.GetPipeline<HttpResponseMessage>("AiServiceBusinessPipeline");

        HttpResponseMessage response;
        try
        {
            response = await pipeline.ExecuteAsync(async state =>
            {
                var json = JsonSerializer.Serialize(request, _jsonSerializerOptions);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                // Execute HTTP call
                return await _httpClient.PostAsync("/api/v1/monitoring/analyze", content, state);
            }, ct);
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException ex)
        {
            throw new AiServiceUnavailableException("AI Service circuit breaker is open. Failing fast.", ex);
        }
        catch (Polly.Timeout.TimeoutRejectedException ex)
        {
            throw new AiServiceUnavailableException("AI Service call timed out.", ex);
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
        {
            _logger.LogError("AI Service monitoring request validation failure (422). Body: {Body}", responseBody);
            throw new AiServiceValidationException("AI Service returned HTTP 422 Unprocessable Entity", responseBody);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("AI Service monitoring request failed with status code {StatusCode}. Body: {Body}", response.StatusCode, responseBody);
            throw new AiServiceUnavailableException($"AI Service returned error status code: {response.StatusCode}");
        }

        MonitoringResponseDto? result;
        try
        {
            result = JsonSerializer.Deserialize<MonitoringResponseDto>(responseBody, _jsonSerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new AiServiceContractException("Deserialization of monitoring response failed", ex);
        }

        if (result == null)
        {
            throw new AiServiceContractException("Deserialized monitoring response was null.");
        }

        // Validate response contract values
        if (result.Confidence < 0.0 || result.Confidence > 1.0)
        {
            throw new AiServiceContractException($"Confidence value {result.Confidence} is out of the allowed [0.0, 1.0] range.");
        }

        if (string.IsNullOrWhiteSpace(result.RiskLevel))
        {
            throw new AiServiceContractException("RiskLevel is missing or empty.");
        }

        if (string.IsNullOrWhiteSpace(result.Route))
        {
            throw new AiServiceContractException("Route is missing or empty.");
        }

        return result;
    }

    public async Task<PricingBatchResponseDto> RecommendPricingAsync(PricingBatchRequestDto request, CancellationToken ct = default)
    {
        _logger.LogInformation("Sending pricing batch recommendations request. CorrelationId: {CorrelationId}", _correlationIdAccessor.GetCorrelationId());

        var pipeline = _pipelineProvider.GetPipeline<HttpResponseMessage>("AiServiceBusinessPipeline");

        HttpResponseMessage response;
        try
        {
            response = await pipeline.ExecuteAsync(async state =>
            {
                var json = JsonSerializer.Serialize(request, _jsonSerializerOptions);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                return await _httpClient.PostAsync("/api/v1/pricing/recommend", content, state);
            }, ct);
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException ex)
        {
            throw new AiServiceUnavailableException("AI Service circuit breaker is open. Failing fast.", ex);
        }
        catch (Polly.Timeout.TimeoutRejectedException ex)
        {
            throw new AiServiceUnavailableException("AI Service call timed out.", ex);
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
        {
            _logger.LogError("AI Service pricing batch validation failure (422). Body: {Body}", responseBody);
            throw new AiServiceValidationException("AI Service returned HTTP 422 Unprocessable Entity", responseBody);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("AI Service pricing batch request failed with status code {StatusCode}. Body: {Body}", response.StatusCode, responseBody);
            throw new AiServiceUnavailableException($"AI Service returned error status code: {response.StatusCode}");
        }

        PricingBatchResponseDto? result;
        try
        {
            result = JsonSerializer.Deserialize<PricingBatchResponseDto>(responseBody, _jsonSerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new AiServiceContractException("Deserialization of pricing recommendation response failed", ex);
        }

        if (result == null)
        {
            throw new AiServiceContractException("Deserialized pricing response was null.");
        }

        // Strict client-side contract validations
        var requestedProductIds = request.Products.Select(p => p.ProductId).ToHashSet();
        var uniqueResponseProductIds = new HashSet<string>();
        var duplicates = new List<string>();

        foreach (var decision in result.Decisions)
        {
            if (string.IsNullOrWhiteSpace(decision.ProductId))
            {
                throw new AiServiceContractException("Decision contains missing or empty ProductId.");
            }

            if (!requestedProductIds.Contains(decision.ProductId))
            {
                throw new AiServiceContractException($"AI pricing decision contains unknown ProductId '{decision.ProductId}' not present in original request.");
            }

            if (!uniqueResponseProductIds.Add(decision.ProductId))
            {
                duplicates.Add(decision.ProductId);
            }

            if (decision.DiscountPercentage < 0.0 || decision.DiscountPercentage > 15.0)
            {
                throw new AiServiceContractException($"DiscountPercentage value {decision.DiscountPercentage} is out of the allowed [0.0, 15.0] range.");
            }

            if (decision.Confidence < 0.0 || decision.Confidence > 1.0)
            {
                throw new AiServiceContractException($"Confidence value {decision.Confidence} is out of the allowed [0.0, 1.0] range.");
            }

            if (string.IsNullOrWhiteSpace(decision.Reason))
            {
                throw new AiServiceContractException("Decision contains missing or empty Reason.");
            }

            if (string.IsNullOrWhiteSpace(decision.ActionRequirement))
            {
                throw new AiServiceContractException("Decision contains missing or empty ActionRequirement.");
            }
        }

        if (duplicates.Count > 0)
        {
            throw new AiServiceContractException($"AI pricing decisions contains duplicate ProductId(s): {string.Join(", ", duplicates)}. Specifically, duplicate ProductId '{string.Join("', '", duplicates)}'.");
        }

        var missing = requestedProductIds.Except(uniqueResponseProductIds).Select(id => id.ToString()).ToList();
        if (missing.Count > 0)
        {
            throw new AiServiceContractException($"AI pricing decisions are missing recommendations for requested ProductId(s): {string.Join(", ", missing)}.");
        }

        if (result.Decisions.Count > request.Products.Count)
        {
            throw new AiServiceContractException($"AI pricing response contains more decisions ({result.Decisions.Count}) than requested products ({request.Products.Count}).");
        }

        return result;
    }

    public async Task<AiServiceHealthDto> GetHealthAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Checking AI Service health. CorrelationId: {CorrelationId}", _correlationIdAccessor.GetCorrelationId());

        var pipeline = _pipelineProvider.GetPipeline<HttpResponseMessage>("AiServiceHealthPipeline");

        HttpResponseMessage response;
        try
        {
            response = await pipeline.ExecuteAsync(async state => 
                await _httpClient.GetAsync("/health", state), ct);
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException ex)
        {
            throw new AiServiceUnavailableException("AI Service circuit breaker is open. Failing fast.", ex);
        }
        catch (Polly.Timeout.TimeoutRejectedException ex)
        {
            throw new AiServiceUnavailableException("AI Service call timed out.", ex);
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new AiServiceUnavailableException($"AI Service health probe failed with status code: {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<AiServiceHealthDto>(responseBody, _jsonSerializerOptions);
        if (result == null)
        {
            throw new AiServiceContractException("Deserialized health response was null.");
        }

        return result;
    }

    public async Task<AiServiceReadyDto> GetReadyAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Checking AI Service readiness. CorrelationId: {CorrelationId}", _correlationIdAccessor.GetCorrelationId());

        var pipeline = _pipelineProvider.GetPipeline<HttpResponseMessage>("AiServiceHealthPipeline");

        HttpResponseMessage response;
        try
        {
            response = await pipeline.ExecuteAsync(async state => 
                await _httpClient.GetAsync("/ready", state), ct);
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException ex)
        {
            throw new AiServiceUnavailableException("AI Service circuit breaker is open. Failing fast.", ex);
        }
        catch (Polly.Timeout.TimeoutRejectedException ex)
        {
            throw new AiServiceUnavailableException("AI Service call timed out.", ex);
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new AiServiceUnavailableException($"AI Service readiness probe failed with status code: {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<AiServiceReadyDto>(responseBody, _jsonSerializerOptions);
        if (result == null)
        {
            throw new AiServiceContractException("Deserialized readiness response was null.");
        }

        return result;
    }

    public async Task<AiServiceVersionDto> GetVersionAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Retrieving AI Service version. CorrelationId: {CorrelationId}", _correlationIdAccessor.GetCorrelationId());

        var pipeline = _pipelineProvider.GetPipeline<HttpResponseMessage>("AiServiceHealthPipeline");

        HttpResponseMessage response;
        try
        {
            response = await pipeline.ExecuteAsync(async state => 
                await _httpClient.GetAsync("/version", state), ct);
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException ex)
        {
            throw new AiServiceUnavailableException("AI Service circuit breaker is open. Failing fast.", ex);
        }
        catch (Polly.Timeout.TimeoutRejectedException ex)
        {
            throw new AiServiceUnavailableException("AI Service call timed out.", ex);
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new AiServiceUnavailableException($"AI Service version call failed with status code: {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<AiServiceVersionDto>(responseBody, _jsonSerializerOptions);
        if (result == null)
        {
            throw new AiServiceContractException("Deserialized version response was null.");
        }

        return result;
    }

    public async Task<HistoricalIngestionResponseDto> IngestHistoricalPricingAsync(HistoricalIngestionRequestDto request, CancellationToken ct = default)
    {
        _logger.LogInformation("Sending historical pricing ingestion request. CorrelationId: {CorrelationId}", _correlationIdAccessor.GetCorrelationId());

        var pipeline = _pipelineProvider.GetPipeline<HttpResponseMessage>("AiServiceBusinessPipeline");

        HttpResponseMessage response;
        try
        {
            response = await pipeline.ExecuteAsync(async state =>
            {
                var json = JsonSerializer.Serialize(request, _jsonSerializerOptions);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                return await _httpClient.PostAsync("/api/v1/pricing/knowledge/ingest", content, state);
            }, ct);
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException ex)
        {
            throw new AiServiceUnavailableException("AI Service circuit breaker is open. Failing fast.", ex);
        }
        catch (Polly.Timeout.TimeoutRejectedException ex)
        {
            throw new AiServiceUnavailableException("AI Service call timed out.", ex);
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
        {
            _logger.LogError("AI Service historical ingestion validation failure (422). Body: {Body}", responseBody);
            throw new AiServiceValidationException("AI Service returned HTTP 422 Unprocessable Entity", responseBody);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("AI Service historical ingestion request failed with status code {StatusCode}. Body: {Body}", response.StatusCode, responseBody);
            throw new AiServiceUnavailableException($"AI Service returned error status code: {response.StatusCode}");
        }

        HistoricalIngestionResponseDto? result;
        try
        {
            result = JsonSerializer.Deserialize<HistoricalIngestionResponseDto>(responseBody, _jsonSerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new AiServiceContractException("Deserialization of historical ingestion response failed", ex);
        }

        if (result == null)
        {
            throw new AiServiceContractException("Deserialized historical ingestion response was null.");
        }

        return result;
    }
}
