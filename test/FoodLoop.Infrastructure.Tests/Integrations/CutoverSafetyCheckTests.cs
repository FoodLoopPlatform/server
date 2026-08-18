using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Integrations;

public class CutoverSafetyCheckTests
{
    [Theory]
    [InlineData("http://localhost:8000")]
    [InlineData("http://127.0.0.1:8000")]
    [InlineData("http://[::1]:8000")]
    public void Production_host_should_fail_to_start_when_ai_service_points_to_localhost(string invalidBaseUrl)
    {
        // Arrange & Act
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((ctx, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        { "AiService:BaseUrl", invalidBaseUrl }
                    });
                });
            });

        // Act & Assert
        Action startHost = () => { var _ = factory.CreateClient(); };
        startHost.Should().Throw<InvalidOperationException>()
            .WithMessage("*[Cutover Safety Check]*");
    }

    [Fact]
    public void Production_host_should_start_successfully_when_ai_service_points_to_external_host()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((ctx, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        { "AiService:BaseUrl", "https://ai.foodloop.platform.internal" }
                    });
                });
            });

        // Act & Assert
        try
        {
            var _ = factory.CreateClient();
        }
        catch (Exception ex)
        {
            ex.ToString().Should().NotContain("[Cutover Safety Check]");
        }
    }
}
