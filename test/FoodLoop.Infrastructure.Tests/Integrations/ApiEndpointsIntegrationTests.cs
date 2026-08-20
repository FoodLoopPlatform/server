using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Integrations;

public class ApiEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Categories_GetCategories_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/categories");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("success");
    }

    [Fact]
    public async Task Charities_GetCharities_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/charities");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("success");
    }

    [Fact]
    public async Task Marketplace_GetProducts_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/marketplace/products");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("success");
    }

    [Fact]
    public async Task Auth_Login_WithInvalidCredentials_ShouldReturnUnauthorized()
    {
        var payload = JsonSerializer.Serialize(new { Email = "nonexistent@test.com", Password = "WrongPassword@123" });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/auth/login", content);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Auth_Register_WithInvalidModel_ShouldReturnBadRequest()
    {
        var payload = JsonSerializer.Serialize(new { Email = "not-an-email", Password = "123" });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/auth/register", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_PendingStores_AnonymousQueue_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/admin/stores/pending");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("success");
    }

    [Fact]
    public async Task ProtectedEndpoints_WithoutToken_ShouldReturnUnauthorized()
    {
        var endpoints = new[]
        {
            "/notifications",
            "/notifications/unread-count",
            "/stores/me",
            "/stores/me/analytics",
            "/stores/me/orders",
            "/stores/me/disputes",
            "/stores/me/disputes/summary",
            "/admin/users",
            "/admin/analytics/summary",
            "/admin/system-settings",
            "/admin/disputes",
            "/orders",
            "/users/me",
            "/users/me/wallet",
            "/users/me/addresses",
            "/users/me/reports"
        };

        foreach (var endpoint in endpoints)
        {
            var response = await _client.GetAsync(endpoint);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"Endpoint {endpoint} must be protected");
        }
    }
}
