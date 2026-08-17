using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FoodLoop.Application.Common.DTOs.AI;
using FoodLoop.Application.Common.Interfaces.AI;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Integrations;

public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthCheckTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_endpoint_should_return_200_OK_and_healthy_when_ai_service_is_ready()
    {
        // Arrange
        var mockClient = new Mock<IAiServiceClient>();
        mockClient.Setup(x => x.GetReadyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiServiceReadyDto("ready"));

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAiServiceClient));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddScoped(_ => mockClient.Object);
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task Health_endpoint_should_return_200_OK_and_degraded_when_ai_service_is_down()
    {
        // Arrange
        var mockClient = new Mock<IAiServiceClient>();
        mockClient.Setup(x => x.GetReadyAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("AI Service Connection Timeout"));

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAiServiceClient));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddScoped(_ => mockClient.Object);
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK); // Must be 200 OK (not 503) for degraded check
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Degraded");
    }
}
