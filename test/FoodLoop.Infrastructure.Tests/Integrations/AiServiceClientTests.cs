using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FoodLoop.Application.Common.DTOs.AI;
using FoodLoop.Application.Common.Exceptions.AI;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Interfaces.AI;
using FoodLoop.Infrastructure.DependencyInjection;
using FoodLoop.Infrastructure.Integrations.AiService;
using FoodLoop.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Polly.CircuitBreaker;
using Polly;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Extensions.Logging;
using Polly.Registry;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Integrations;

public class AiServiceClientTests
{
    private readonly Mock<ICorrelationIdAccessor> _mockCorrelationIdAccessor;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly string _correlationId = "TEST-CORRELATION-ID-777";

    public AiServiceClientTests()
    {
        _mockCorrelationIdAccessor = new Mock<ICorrelationIdAccessor>();
        _mockCorrelationIdAccessor.Setup(x => x.GetCorrelationId()).Returns(_correlationId);

        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
    }

    private ServiceProvider BuildServiceProvider(HttpMessageHandler handler, string baseUrl = "http://localhost:8000", TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AiService:BaseUrl", baseUrl },
                { "AiService:TimeoutSeconds", "10" }
            })
            .Build();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(_mockCorrelationIdAccessor.Object);
        services.AddTransient<CorrelationIdDelegatingHandler>();

        // Register custom resilience pipelines and options
        services.AddOptions<AiServiceOptions>()
            .Bind(configuration.GetSection(AiServiceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // 1. Business Resilience Pipeline (Retry, Timeout, Circuit Breaker)
        services.AddResiliencePipeline<string, HttpResponseMessage>("AiServiceBusinessPipeline", builder =>
        {
            if (timeProvider != null)
            {
                builder.TimeProvider = timeProvider;
            }

            builder.AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Constant,
                Delay = timeProvider != null ? TimeSpan.Zero : TimeSpan.FromMilliseconds(10), // Prevent hangs in virtual time tests
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<Polly.Timeout.TimeoutRejectedException>()
                    .HandleResult(response => (int)response.StatusCode >= 500)
            });

            builder.AddTimeout(new Polly.Timeout.TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(2)
            });

            builder.AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(10),
                MinimumThroughput = 4,
                BreakDuration = TimeSpan.FromMilliseconds(500),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<Polly.Timeout.TimeoutRejectedException>()
                    .HandleResult(response => (int)response.StatusCode >= 500)
            });
        });

        // 2. Health check Resilience Pipeline
        services.AddResiliencePipeline<string, HttpResponseMessage>("AiServiceHealthPipeline", builder =>
        {
            if (timeProvider != null)
            {
                builder.TimeProvider = timeProvider;
            }

            builder.AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 1,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.FromMilliseconds(5),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(response => (int)response.StatusCode >= 500)
            });

            builder.AddTimeout(new Polly.Timeout.TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromMilliseconds(100) // Shorter timeout for testing
            });
        });

        services.AddHttpClient<IAiServiceClient, AiServiceClient>((sp, client) =>
        {
            client.BaseAddress = new Uri(baseUrl);
        })
        .ConfigurePrimaryHttpMessageHandler(() => handler)
        .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task AnalyzeMonitoringAsync_happy_path_should_propagate_correlation_id_and_parse_snake_case()
    {
        // Arrange
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(@"{ ""route"": ""PRICING"", ""risk_level"": ""CRITICAL"", ""reason"": ""Severe exposure"", ""confidence"": 0.95 }")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(mockResponse);

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new MonitoringRequestDto(
            Product: new("p-10", "Milk", "Dairy"),
            Inventory: new(10, 20.00m, 18.00m, 15.00m),
            Demand: new(2.5, new(3.0)),
            Expiry: new(DateTimeOffset.UtcNow.AddHours(2), 2.0),
            Location: new(30.05, 31.23, "store-cairo"),
            StorePolicy: new("store-cairo", "assisted"),
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act
        var result = await client.AnalyzeMonitoringAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Route.Should().Be("PRICING");
        result.RiskLevel.Should().Be("CRITICAL");
        result.Confidence.Should().Be(0.95);

        // Verify request verification
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Headers.GetValues("X-Correlation-ID").Contains(_correlationId) &&
                req.RequestUri!.AbsolutePath == "/api/v1/monitoring/analyze"
            ),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task RecommendPricingAsync_happy_path_should_succeed()
    {
        // Arrange
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(@"{ ""store_id"": ""store-cairo"", ""decisions"": [ { ""product_id"": ""p-10"", ""discount_percentage"": 12.5, ""reason"": ""Expiry near"", ""confidence"": 0.9, ""action_requirement"": ""APPROVAL_REQUIRED"", ""action_reason"": ""Assisted Mode"" } ] }")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(mockResponse);

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new PricingBatchRequestDto(
            StoreId: "store-cairo",
            StorePolicy: new("store-cairo", "assisted"),
            Products: new List<PricingProductRequestDto>
            {
                new("p-10", "Milk", "Dairy", new(10, 20m, 18m, 15m), new(2.0, new(3.0)), new(DateTimeOffset.UtcNow, 2.0), new("CRITICAL", "Near expiry", 0.95))
            }
        );

        // Act
        var result = await client.RecommendPricingAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.StoreId.Should().Be("store-cairo");
        result.Decisions.Should().HaveCount(1);
        result.Decisions[0].ProductId.Should().Be("p-10");
        result.Decisions[0].DiscountPercentage.Should().Be(12.5);
    }

    [Fact]
    public async Task GetHealthAsync_and_GetReadyAsync_should_succeed_and_deserialize_permissively()
    {
        // Arrange
        var mockHealthResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(@"{ ""status"": ""healthy"", ""extra_info_ignored"": true }")
        };
        var mockReadyResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(@"{ ""status"": ""ready"", ""database"": ""connected"" }")
        };

        var handlerSetup = _mockHttpMessageHandler.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );

        handlerSetup.ReturnsAsync(mockHealthResponse);
        handlerSetup.ReturnsAsync(mockReadyResponse);

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        // Act
        var health = await client.GetHealthAsync();
        var ready = await client.GetReadyAsync();

        // Assert
        health.Status.Should().Be("healthy");
        ready.Status.Should().Be("ready");
    }

    [Fact]
    public async Task Request_422_should_throw_AiServiceValidationException()
    {
        // Arrange
        var mockResponse = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent(@"{ ""detail"": [ { ""loc"": [ ""body"", ""timestamp"" ], ""msg"": ""invalid format"" } ] }")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(mockResponse);

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new MonitoringRequestDto(
            Product: new("p-10", "Milk", "Dairy"),
            Inventory: new(10, 20.00m, 18.00m, 15.00m),
            Demand: new(2.5, new(3.0)),
            Expiry: new(DateTimeOffset.UtcNow.AddHours(2), 2.0),
            Location: new(30.05, 31.23, "store-cairo"),
            StorePolicy: new("store-cairo", "assisted"),
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AiServiceValidationException>(() => client.AnalyzeMonitoringAsync(request));
        ex.RawResponseBody.Should().Contain("loc").And.Contain("invalid format");
    }

    [Fact]
    public async Task Transient_fault_should_retry_and_eventually_succeed()
    {
        // Arrange
        var seq = _mockHttpMessageHandler.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );

        // Mock 2 failures (HTTP 500) followed by 1 success
        seq.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        seq.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        seq.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(@"{ ""route"": ""NO_ACTION"", ""risk_level"": ""LOW"", ""reason"": ""Safe"", ""confidence"": 0.9 }")
        });

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new MonitoringRequestDto(
            Product: new("p-10", "Milk", "Dairy"),
            Inventory: new(10, 20.00m, 18.00m, 15.00m),
            Demand: new(2.5, new(3.0)),
            Expiry: new(DateTimeOffset.UtcNow.AddHours(2), 2.0),
            Location: new(30.05, 31.23, "store-cairo"),
            StorePolicy: new("store-cairo", "assisted"),
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act
        var result = await client.AnalyzeMonitoringAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Route.Should().Be("NO_ACTION");
    }

    [Fact]
    public async Task Consistent_faults_should_exhaust_retries_and_throw_AiServiceUnavailableException()
    {
        // Arrange
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Returns(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new MonitoringRequestDto(
            Product: new("p-10", "Milk", "Dairy"),
            Inventory: new(10, 20.00m, 18.00m, 15.00m),
            Demand: new(2.5, new(3.0)),
            Expiry: new(DateTimeOffset.UtcNow.AddHours(2), 2.0),
            Location: new(30.05, 31.23, "store-cairo"),
            StorePolicy: new("store-cairo", "assisted"),
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act & Assert
        await Assert.ThrowsAsync<AiServiceUnavailableException>(() => client.AnalyzeMonitoringAsync(request));
    }

    [Fact]
    public async Task Circuit_breaker_should_open_after_failures_and_fail_fast_on_next_calls()
    {
        // Arrange
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Returns(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new MonitoringRequestDto(
            Product: new("p-10", "Milk", "Dairy"),
            Inventory: new(10, 20.00m, 18.00m, 15.00m),
            Demand: new(2.5, new(3.0)),
            Expiry: new(DateTimeOffset.UtcNow.AddHours(2), 2.0),
            Location: new(30.05, 31.23, "store-cairo"),
            StorePolicy: new("store-cairo", "assisted"),
            Timestamp: DateTimeOffset.UtcNow
        );

        // 1. Trigger failures to trip the circuit breaker (minimum throughput 4, failure ratio 50%)
        for (int i = 0; i < 4; i++)
        {
            try
            {
                await client.AnalyzeMonitoringAsync(request);
            }
            catch (AiServiceUnavailableException) { }
        }

        // 2. Next call must immediately fail fast due to open circuit breaker
        var ex = await Assert.ThrowsAsync<AiServiceUnavailableException>(() => client.AnalyzeMonitoringAsync(request));
        ex.Message.Should().Contain("circuit breaker is open");
    }

    [Fact]
    public async Task Response_with_unknown_product_id_should_throw_AiServiceContractException()
    {
        // Arrange
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            // Returns decision for product "p-99" which is not in the original request ("p-10")
            Content = new StringContent(@"{ ""store_id"": ""store-cairo"", ""decisions"": [ { ""product_id"": ""p-99"", ""discount_percentage"": 5.0, ""reason"": ""Unknown item"", ""confidence"": 0.9, ""action_requirement"": ""APPROVAL_REQUIRED"", ""action_reason"": ""unknown"" } ] }")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(mockResponse);

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new PricingBatchRequestDto(
            StoreId: "store-cairo",
            StorePolicy: new("store-cairo", "assisted"),
            Products: new List<PricingProductRequestDto>
            {
                new("p-10", "Milk", "Dairy", new(10, 20m, 18m, 15m), new(2.0, new(3.0)), new(DateTimeOffset.UtcNow, 2.0), new("CRITICAL", "Near expiry", 0.95))
            }
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AiServiceContractException>(() => client.RecommendPricingAsync(request));
        ex.Message.Should().Contain("unknown ProductId 'p-99'");
    }

    [Fact]
    public async Task RecommendPricingAsync_boundary_values_0_and_15_discount_and_0_and_1_confidence_should_succeed()
    {
        // Scenario: discount exactly 0.0 and 15.0, confidence exactly 0.0 and 1.0 (inclusive boundaries).
        // Finding: confirms client accepts closed-interval boundary values.
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(@"{
                ""store_id"": ""store-cairo"",
                ""decisions"": [
                    { ""product_id"": ""p-10"", ""discount_percentage"": 0.0, ""reason"": ""No change"", ""confidence"": 0.0, ""action_requirement"": ""APPROVAL_REQUIRED"", ""action_reason"": ""min"" },
                    { ""product_id"": ""p-11"", ""discount_percentage"": 15.0, ""reason"": ""Max allowed"", ""confidence"": 1.0, ""action_requirement"": ""APPROVAL_REQUIRED"", ""action_reason"": ""max"" }
                ]
            }")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(mockResponse);

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new PricingBatchRequestDto(
            StoreId: "store-cairo",
            StorePolicy: new("store-cairo", "assisted"),
            Products: new List<PricingProductRequestDto>
            {
                new("p-10", "Milk", "Dairy", new(10, 20m, 18m, 15m), new(2.0, new(3.0)), new(DateTimeOffset.UtcNow, 2.0), new("CRITICAL", "Near expiry", 0.95)),
                new("p-11", "Bread", "Bakery", new(5, 10m, 9m, 8m), new(1.0, new(2.0)), new(DateTimeOffset.UtcNow, 2.0), new("HIGH", "Low velocity", 0.85))
            }
        );

        var result = await client.RecommendPricingAsync(request);

        result.Decisions.Should().HaveCount(2);
        result.Decisions[0].DiscountPercentage.Should().Be(0.0);
        result.Decisions[0].Confidence.Should().Be(0.0);
        result.Decisions[1].DiscountPercentage.Should().Be(15.0);
        result.Decisions[1].Confidence.Should().Be(1.0);
    }

    [Fact]
    public async Task RecommendPricingAsync_duplicate_product_id_in_response_should_throw_AiServiceContractException()
    {
        // Scenario: AI returns two decisions for the same product ID that was in the request.
        // Finding: confirms client rejects duplicate ProductId before handler sees the response.
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(@"{
                ""store_id"": ""store-cairo"",
                ""decisions"": [
                    { ""product_id"": ""p-10"", ""discount_percentage"": 5.0, ""reason"": ""First"", ""confidence"": 0.9, ""action_requirement"": ""APPROVAL_REQUIRED"", ""action_reason"": ""dup"" },
                    { ""product_id"": ""p-10"", ""discount_percentage"": 10.0, ""reason"": ""Duplicate"", ""confidence"": 0.8, ""action_requirement"": ""APPROVAL_REQUIRED"", ""action_reason"": ""dup"" }
                ]
            }")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(mockResponse);

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new PricingBatchRequestDto(
            StoreId: "store-cairo",
            StorePolicy: new("store-cairo", "assisted"),
            Products: new List<PricingProductRequestDto>
            {
                new("p-10", "Milk", "Dairy", new(10, 20m, 18m, 15m), new(2.0, new(3.0)), new(DateTimeOffset.UtcNow, 2.0), new("CRITICAL", "Near expiry", 0.95))
            }
        );

        var ex = await Assert.ThrowsAsync<AiServiceContractException>(() => client.RecommendPricingAsync(request));
        ex.Message.Should().Contain("duplicate ProductId 'p-10'");
    }

    [Fact]
    public async Task AnalyzeMonitoringAsync_boundary_confidence_0_and_1_should_succeed()
    {
        // Scenario: monitoring response with confidence exactly 0.0 and exactly 1.0.
        // Finding: confirms inclusive [0.0, 1.0] boundary on monitoring endpoint.
        foreach (var confidence in new[] { 0.0, 1.0 })
        {
            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($@"{{ ""route"": ""NO_ACTION"", ""risk_level"": ""LOW"", ""reason"": ""Boundary"", ""confidence"": {confidence.ToString(System.Globalization.CultureInfo.InvariantCulture)} }}")
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(mockResponse);

            var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
            var client = sp.GetRequiredService<IAiServiceClient>();

            var request = new MonitoringRequestDto(
                Product: new("p-10", "Milk", "Dairy"),
                Inventory: new(10, 20.00m, 18.00m, 15.00m),
                Demand: new(2.5, new(3.0)),
                Expiry: new(DateTimeOffset.UtcNow.AddHours(2), 2.0),
                Location: new(30.05, 31.23, "store-cairo"),
                StorePolicy: new("store-cairo", "assisted"),
                Timestamp: DateTimeOffset.UtcNow
            );

            var result = await client.AnalyzeMonitoringAsync(request);
            result.Confidence.Should().Be(confidence);
        }
    }

    [Fact]
    public async Task Response_with_out_of_bounds_discount_percentage_should_throw_AiServiceContractException()
    {
        // Arrange
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            // Discount percentage 15.01 exceeds maximum [0,15] limit
            Content = new StringContent(@"{ ""store_id"": ""store-cairo"", ""decisions"": [ { ""product_id"": ""p-10"", ""discount_percentage"": 15.01, ""reason"": ""Too high"", ""confidence"": 0.9, ""action_requirement"": ""APPROVAL_REQUIRED"", ""action_reason"": ""out of bounds"" } ] }")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(mockResponse);

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new PricingBatchRequestDto(
            StoreId: "store-cairo",
            StorePolicy: new("store-cairo", "assisted"),
            Products: new List<PricingProductRequestDto>
            {
                new("p-10", "Milk", "Dairy", new(10, 20m, 18m, 15m), new(2.0, new(3.0)), new(DateTimeOffset.UtcNow, 2.0), new("CRITICAL", "Near expiry", 0.95))
            }
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AiServiceContractException>(() => client.RecommendPricingAsync(request));
        ex.Message.Should().Contain("DiscountPercentage value 15.01 is out of the allowed");
    }

    [Fact]
    public async Task Options_validation_fails_fast_when_missing_base_url()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // AiService:BaseUrl is missing
                { "AiService:TimeoutSeconds", "10" }
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<AiServiceOptions>()
            .Bind(configuration.GetSection(AiServiceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var sp = services.BuildServiceProvider();

        // Act & Assert
        Assert.Throws<OptionsValidationException>(() => sp.GetRequiredService<IOptions<AiServiceOptions>>().Value);
    }

    [Fact]
    public async Task GetHealthAsync_should_timeout_rapidly_under_health_pipeline_policy()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Returns((HttpRequestMessage request, CancellationToken ct) =>
            {
                var tcs = new TaskCompletionSource<HttpResponseMessage>();
                ct.Register(() => tcs.TrySetCanceled(ct));
                return tcs.Task;
            });

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object, timeProvider: fakeTimeProvider);
        var client = sp.GetRequiredService<IAiServiceClient>();

        // Act
        var clientTask = client.GetHealthAsync();

        // Advance the fake time past the 100ms timeout
        fakeTimeProvider.Advance(TimeSpan.FromMilliseconds(150));

        // Assert
        var ex = await Assert.ThrowsAsync<AiServiceUnavailableException>(async () => await clientTask);
        ex.Message.Should().Contain("timed out");
    }

    [Fact]
    public async Task AnalyzeMonitoringAsync_should_not_timeout_under_4s_delay_since_business_timeout_is_longer()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var tcs = new TaskCompletionSource<HttpResponseMessage>();

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Returns((HttpRequestMessage request, CancellationToken ct) =>
            {
                ct.Register(() => tcs.TrySetCanceled(ct));
                return tcs.Task;
            });

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object, timeProvider: fakeTimeProvider);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new MonitoringRequestDto(
            Product: new("p-10", "Milk", "Dairy"),
            Inventory: new(10, 20.00m, 18.00m, 15.00m),
            Demand: new(2.5, new(3.0)),
            Expiry: new(DateTimeOffset.UtcNow.AddHours(2), 2.0),
            Location: new(30.05, 31.23, "store-cairo"),
            StorePolicy: new("store-cairo", "assisted"),
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act
        var clientTask = client.AnalyzeMonitoringAsync(request);

        // Advance time by 150ms (exceeds health timeout of 100ms, but well below business timeout of 2s)
        fakeTimeProvider.Advance(TimeSpan.FromMilliseconds(150));

        // Complete the task now
        tcs.TrySetResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(@"{""route"":""NO_ACTION"",""risk_level"":""LOW"",""reason"":""OK"",""confidence"":0.8}")
        });

        var result = await clientTask;

        // Assert
        result.Should().NotBeNull();
        result.Route.Should().Be("NO_ACTION");
    }

    [Fact]
    public async Task GetHealthAsync_should_retry_at_most_once_under_health_pipeline_policy()
    {
        // Arrange
        var requestCount = 0;
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Returns(() =>
            {
                requestCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            });

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        // Act
        try
        {
            await client.GetHealthAsync();
        }
        catch (AiServiceUnavailableException) { }

        // Assert - MaxRetryAttempts = 1 means 2 total attempts (1 initial + 1 retry)
        requestCount.Should().Be(2);
    }

    [Fact]
    public async Task AnalyzeMonitoringAsync_should_retry_three_times_under_business_pipeline_policy()
    {
        // Arrange
        var requestCount = 0;
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Returns(() =>
            {
                requestCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            });

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new MonitoringRequestDto(
            Product: new("p-10", "Milk", "Dairy"),
            Inventory: new(10, 20.00m, 18.00m, 15.00m),
            Demand: new(2.5, new(3.0)),
            Expiry: new(DateTimeOffset.UtcNow.AddHours(2), 2.0),
            Location: new(30.05, 31.23, "store-cairo"),
            StorePolicy: new("store-cairo", "assisted"),
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act
        try
        {
            await client.AnalyzeMonitoringAsync(request);
        }
        catch (AiServiceUnavailableException) { }

        // Assert - MaxRetryAttempts = 3 means 4 total attempts (1 initial + 3 retries)
        requestCount.Should().Be(4);
    }

    [Fact]
    public async Task CircuitBreaker_should_trip_fail_fast_and_recover_after_cooldown_using_virtual_time()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var isFailure = true;

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Returns(() =>
            {
                if (isFailure)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
                }
                else
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(@"{""route"":""NO_ACTION"",""risk_level"":""LOW"",""reason"":""OK"",""confidence"":0.9}")
                    });
                }
            });

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object, timeProvider: fakeTimeProvider);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new MonitoringRequestDto(
            Product: new("p-10", "Milk", "Dairy"),
            Inventory: new(10, 20.00m, 18.00m, 15.00m),
            Demand: new(2.5, new(3.0)),
            Expiry: new(DateTimeOffset.UtcNow.AddHours(2), 2.0),
            Location: new(30.05, 31.23, "store-cairo"),
            StorePolicy: new("store-cairo", "assisted"),
            Timestamp: DateTimeOffset.UtcNow
        );

        // 1. Send 5 failed requests to trigger circuit breaker.
        // Note: This unit test intentionally registers and uses a looser local pipeline configuration
        // (MinimumThroughput = 4) than production (MinimumThroughput = 3) to test client class resilience logic in isolation.
        for (int i = 0; i < 5; i++)
        {
            try
            {
                await client.AnalyzeMonitoringAsync(request);
            }
            catch (AiServiceUnavailableException)
            {
                // Expected failures
            }
        }

        // 2. The 6th request should fail fast with a circuit breaker open exception
        var ex = await Assert.ThrowsAsync<AiServiceUnavailableException>(async () => await client.AnalyzeMonitoringAsync(request));
        ex.Message.Should().Contain("circuit"); // Asserts breaker open state

        // 3. Advance fake clock past the 30 seconds cooldown duration
        isFailure = false; // Next call should succeed to close the breaker
        fakeTimeProvider.Advance(TimeSpan.FromSeconds(31));

        // 4. Send request - it should attempt to call (half-open) and succeed (transitioning back to closed)
        var result = await client.AnalyzeMonitoringAsync(request);
        result.Should().NotBeNull();
        result.Route.Should().Be("NO_ACTION");

        // 5. Subsequent request should work directly and succeed
        var result2 = await client.AnalyzeMonitoringAsync(request);
        result2.Should().NotBeNull();
    }

    [Fact]
    public void CorrelationIdAccessor_GetCorrelationId_should_return_fallback_uuid_when_httpContext_is_null()
    {
        // Arrange
        var mockHttpContextAccessor = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((Microsoft.AspNetCore.Http.HttpContext)null!);

        var accessor = new FoodLoop.Infrastructure.Services.CorrelationIdAccessor(mockHttpContextAccessor.Object);

        // Act
        var id1 = accessor.GetCorrelationId();
        var id2 = accessor.GetCorrelationId();

        // Assert
        id1.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(id1, out _).Should().BeTrue();
        id2.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(id2, out _).Should().BeTrue();

        // Ensure uniqueness (non-repeating)
        id1.Should().NotBe(id2);
    }

    [Fact]
    public async Task RecommendPricingAsync_counts_match_but_returned_IDs_are_duplicates_should_throw_AiServiceContractException()
    {
        // Scenario: Requested 2 products, AI returns 2 decisions but duplicates the same product ID (count matches but 1:1 mapping fails).
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(@"{
                ""store_id"": ""store-cairo"",
                ""decisions"": [
                    { ""product_id"": ""p-10"", ""discount_percentage"": 5.0, ""reason"": ""First"", ""confidence"": 0.9, ""action_requirement"": ""APPROVAL_REQUIRED"", ""action_reason"": ""dup"" },
                    { ""product_id"": ""p-10"", ""discount_percentage"": 10.0, ""reason"": ""Duplicate"", ""confidence"": 0.8, ""action_requirement"": ""APPROVAL_REQUIRED"", ""action_reason"": ""dup"" }
                ]
            }")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(mockResponse);

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new PricingBatchRequestDto(
            StoreId: "store-cairo",
            StorePolicy: new("store-cairo", "assisted"),
            Products: new List<PricingProductRequestDto>
            {
                new("p-10", "Milk", "Dairy", new(10, 20m, 18m, 15m), new(2.0, new(3.0)), new(DateTimeOffset.UtcNow, 2.0), new("CRITICAL", "Near expiry", 0.95)),
                new("p-11", "Bread", "Bakery", new(5, 10m, 9m, 8m), new(1.0, new(2.0)), new(DateTimeOffset.UtcNow, 2.0), new("HIGH", "Low velocity", 0.85))
            }
        );

        var ex = await Assert.ThrowsAsync<AiServiceContractException>(() => client.RecommendPricingAsync(request));
        ex.Message.Should().Contain("duplicate ProductId(s)");
    }

    [Fact]
    public async Task RecommendPricingAsync_fewer_decisions_than_products_should_throw_AiServiceContractException()
    {
        // Scenario: Requested 2 products, AI only returns 1 decision.
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(@"{
                ""store_id"": ""store-cairo"",
                ""decisions"": [
                    { ""product_id"": ""p-10"", ""discount_percentage"": 5.0, ""reason"": ""First"", ""confidence"": 0.9, ""action_requirement"": ""APPROVAL_REQUIRED"", ""action_reason"": ""fewer"" }
                ]
            }")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(mockResponse);

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new PricingBatchRequestDto(
            StoreId: "store-cairo",
            StorePolicy: new("store-cairo", "assisted"),
            Products: new List<PricingProductRequestDto>
            {
                new("p-10", "Milk", "Dairy", new(10, 20m, 18m, 15m), new(2.0, new(3.0)), new(DateTimeOffset.UtcNow, 2.0), new("CRITICAL", "Near expiry", 0.95)),
                new("p-11", "Bread", "Bakery", new(5, 10m, 9m, 8m), new(1.0, new(2.0)), new(DateTimeOffset.UtcNow, 2.0), new("HIGH", "Low velocity", 0.85))
            }
        );

        var ex = await Assert.ThrowsAsync<AiServiceContractException>(() => client.RecommendPricingAsync(request));
        ex.Message.Should().Contain("missing recommendations for requested ProductId(s)");
    }

    [Fact]
    public async Task RecommendPricingAsync_more_decisions_than_products_should_throw_AiServiceContractException()
    {
        // Scenario: Requested 1 product, AI returns 2 decisions.
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(@"{
                ""store_id"": ""store-cairo"",
                ""decisions"": [
                    { ""product_id"": ""p-10"", ""discount_percentage"": 5.0, ""reason"": ""First"", ""confidence"": 0.9, ""action_requirement"": ""APPROVAL_REQUIRED"", ""action_reason"": ""more"" },
                    { ""product_id"": ""p-11"", ""discount_percentage"": 10.0, ""reason"": ""Extra"", ""confidence"": 0.8, ""action_requirement"": ""APPROVAL_REQUIRED"", ""action_reason"": ""more"" }
                ]
            }")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(mockResponse);

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new PricingBatchRequestDto(
            StoreId: "store-cairo",
            StorePolicy: new("store-cairo", "assisted"),
            Products: new List<PricingProductRequestDto>
            {
                new("p-10", "Milk", "Dairy", new(10, 20m, 18m, 15m), new(2.0, new(3.0)), new(DateTimeOffset.UtcNow, 2.0), new("CRITICAL", "Near expiry", 0.95))
            }
        );

        var ex = await Assert.ThrowsAsync<AiServiceContractException>(() => client.RecommendPricingAsync(request));
        ex.Message.Should().Contain("unknown ProductId 'p-11'");
    }

    [Theory]
    [InlineData(-0.0001)]
    [InlineData(1.0001)]
    public async Task RecommendPricingAsync_out_of_bounds_confidence_values_should_throw_AiServiceContractException(double invalidConfidence)
    {
        var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($@"[{{
                ""store_id"": ""store-cairo"",
                ""decisions"": [
                    {{ ""product_id"": ""p-10"", ""discount_percentage"": 5.0, ""reason"": ""Invalid confidence"", ""confidence"": {invalidConfidence}, ""action_requirement"": ""APPROVAL_REQUIRED"", ""action_reason"": ""bounds"" }}
                ]
            }}]")
        };

        // Standard JSON format for batch response
        var jsonResponse = $@"{{
            ""store_id"": ""store-cairo"",
            ""decisions"": [
                {{ ""product_id"": ""p-10"", ""discount_percentage"": 5.0, ""reason"": ""Invalid confidence"", ""confidence"": {invalidConfidence}, ""action_requirement"": ""APPROVAL_REQUIRED"", ""action_reason"": ""bounds"" }}
            ]
        }}";

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(response);

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new PricingBatchRequestDto(
            StoreId: "store-cairo",
            StorePolicy: new("store-cairo", "assisted"),
            Products: new List<PricingProductRequestDto>
            {
                new("p-10", "Milk", "Dairy", new(10, 20m, 18m, 15m), new(2.0, new(3.0)), new(DateTimeOffset.UtcNow, 2.0), new("CRITICAL", "Near expiry", 0.95))
            }
        );

        var ex = await Assert.ThrowsAsync<AiServiceContractException>(() => client.RecommendPricingAsync(request));
        ex.Message.Should().Contain("Confidence value");
    }

    [Fact]
    public async Task AiServiceClient_logs_should_never_contain_raw_HTTP_headers_at_any_log_level()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AiServiceClient>>();
        var loggedMessages = new List<string>();

        mockLogger.Setup(x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()))
            .Callback(new InvocationAction(invocation =>
            {
                var state = invocation.Arguments[2];
                var exception = (Exception?)invocation.Arguments[3];
                var formatter = invocation.Arguments[4];
                
                var formatterType = formatter.GetType();
                var methodInfo = formatterType.GetMethod("Invoke");
                if (methodInfo != null)
                {
                    var formattedMessage = methodInfo.Invoke(formatter, new[] { state, exception }) as string;
                    if (formattedMessage != null)
                    {
                        loggedMessages.Add(formattedMessage);
                    }
                }
            }));

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{ ""route"": ""PRICING"", ""risk_level"": ""CRITICAL"", ""reason"": ""Severe exposure"", ""confidence"": 0.95 }")
            });

        using var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8000")
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "samba-nova-super-secret-key-12345");
        httpClient.DefaultRequestHeaders.Add("api-key", "some-other-secret-key");

        // Mock resilience pipelines
        var mockPipelineProvider = new Mock<ResiliencePipelineProvider<string>>();
        mockPipelineProvider.Setup(x => x.GetPipeline<HttpResponseMessage>("AiServiceBusinessPipeline"))
            .Returns(ResiliencePipeline<HttpResponseMessage>.Empty);

        var client = new AiServiceClient(httpClient, mockPipelineProvider.Object, mockLogger.Object, _mockCorrelationIdAccessor.Object);

        var request = new MonitoringRequestDto(
            Product: new("p-10", "Milk", "Dairy"),
            Inventory: new(10, 20.00m, 18.00m, 15.00m),
            Demand: new(2.5, new(3.0)),
            Expiry: new(DateTimeOffset.UtcNow.AddHours(2), 2.0),
            Location: new(30.05, 31.23, "store-cairo"),
            StorePolicy: new("store-cairo", "assisted"),
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act
        var result = await client.AnalyzeMonitoringAsync(request);

        // Assert
        result.Should().NotBeNull();
        loggedMessages.Should().NotBeEmpty();
        foreach (var msg in loggedMessages)
        {
            msg.Should().NotContain("samba-nova-super-secret-key-12345");
            msg.Should().NotContain("some-other-secret-key");
            msg.Should().NotContain("Bearer");
            msg.Should().NotContain("Authorization");
            msg.Should().NotContain("api-key");
        }
    }

    [Fact]
    public async Task GetVersionAsync_should_call_version_endpoint_and_return_version_dto()
    {
        // Arrange
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{ ""name"": ""Banana AI Service"", ""version"": ""1.0.4"", ""environment"": ""production"" }")
            });

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        // Act
        var result = await client.GetVersionAsync();

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Banana AI Service");
        result.Version.Should().Be("1.0.4");
        result.Environment.Should().Be("production");
    }

    [Fact]
    public async Task IngestHistoricalPricingAsync_should_call_ingest_endpoint_and_return_ingestion_response_dto()
    {
        // Arrange
        var mockResponseContent = @"{
            ""accepted_count"": 2,
            ""upserted_count"": 2,
            ""failed_count"": 0,
            ""document_ids"": [""doc-1"", ""doc-2""]
        }";

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(mockResponseContent)
            });

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new HistoricalIngestionRequestDto(new List<HistoricalPricingEventDto>
        {
            new HistoricalPricingEventDto(
                EventId: "ev-1", StoreId: "store-1", ProductId: "prod-1", Category: "Fruits",
                RecordedAt: DateTimeOffset.UtcNow, Quantity: 10, CurrentPrice: 15.00m, OriginalPrice: 20.00m,
                PriceFloor: 10.00m, SalesVelocity: 1.5, HistoricalAverageDailySales: 2.0, HoursRemaining: 24.0,
                DiscountPercentage: 10.0, UnitsSoldAfterDiscount: 8, SellThroughRate: 0.8, Outcome: "SOLD_OUT"
            )
        });

        // Act
        var result = await client.IngestHistoricalPricingAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.AcceptedCount.Should().Be(2);
        result.UpsertedCount.Should().Be(2);
        result.FailedCount.Should().Be(0);
        result.DocumentIds.Should().Contain("doc-1");
    }

    [Fact]
    public async Task IngestHistoricalPricingAsync_should_throw_AiServiceValidationException_on_422()
    {
        // Arrange
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent(@"{""detail"": ""Validation error details""}")
            });

        var sp = BuildServiceProvider(_mockHttpMessageHandler.Object);
        var client = sp.GetRequiredService<IAiServiceClient>();

        var request = new HistoricalIngestionRequestDto(new List<HistoricalPricingEventDto>());

        // Act
        var act = async () => await client.IngestHistoricalPricingAsync(request);

        // Assert
        var exception = await act.Should().ThrowAsync<AiServiceValidationException>();
        exception.Which.RawResponseBody.Should().Contain("Validation error details");
    }
}
