using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FoodLoop.API.Controllers;
using FoodLoop.API.Middleware;
using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.AiIntegration.Commands;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.AiIntegration;

public class AiRecommendationsControllerIntegrationTests
{
    private class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[] {
                new Claim(ClaimTypes.Name, "Test Merchant"),
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Merchant")
            };
            var identity = new ClaimsIdentity(claims, "TestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestScheme");

            var result = AuthenticateResult.Success(ticket);
            return Task.FromResult(result);
        }
    }

    [Fact]
    public async Task Post_Approve_Recommendation_From_Another_Store_Should_Return_Forbidden_403()
    {
        // Arrange
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());
        mockCurrentUser.Setup(u => u.Roles).Returns(new List<string> { "Merchant" });

        var mockMediator = new Mock<IMediator>();
        // Make IMediator throw UnauthorizedAccessException when approving recommendation
        mockMediator.Setup(m => m.Send<Result<Unit>>(It.IsAny<ApproveAiRecommendationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Merchant is not authorized to act on another store's recommendation."));

        var mockLoc = new Mock<ILocalizationService>();

        var builder = new WebHostBuilder()
            .UseEnvironment("Testing")
            .ConfigureAppConfiguration((ctx, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>());
            })
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddRouting();
                services.AddAuthorization();
                services.AddControllers()
                    .AddApplicationPart(typeof(AiRecommendationsController).Assembly);

                // Configure test authentication scheme
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "TestScheme";
                    options.DefaultChallengeScheme = "TestScheme";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", options => { });

                services.AddSingleton(mockCurrentUser.Object);
                services.AddSingleton(mockMediator.Object);
                services.AddSingleton(mockLoc.Object);
            })
            .Configure(app =>
            {
                app.UseMiddleware<ExceptionHandlingMiddleware>();
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });
            });

        using var server = new TestServer(builder);
        using var client = server.CreateClient();

        // Add scheme authorization header
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("TestScheme");

        // Act
        var response = await client.PostAsync($"/stores/me/ai-recommendations/{Guid.NewGuid()}/approve", null);

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"Response content was: {content}");

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object?>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized.");
    }
}
