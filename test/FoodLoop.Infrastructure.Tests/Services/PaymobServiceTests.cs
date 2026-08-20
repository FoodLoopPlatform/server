using FluentAssertions;
using FoodLoop.Infrastructure.Options;
using FoodLoop.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Services;

public class PaymobServiceTests
{
    [Fact]
    public async Task GeneratePaymentTokenAsync_ValidRequest_ShouldReturnClientSecret()
    {
        // Arrange
        var mockHttpHandler = new Mock<HttpMessageHandler>();
        var responseObj = new { client_secret = "secret_token_12345" };
        var jsonResponse = JsonSerializer.Serialize(responseObj);

        mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var options = Microsoft.Extensions.Options.Options.Create(new PaymobOptions
        {
            ApiKey = "test_api_key",
            IntegrationId = "12345",
            BaseUrl = "https://accept.paymob.com",
            HmacSecret = "test_hmac_secret"
        });

        var service = new PaymobService(httpClient, options);
        var orderId = Guid.NewGuid();

        // Act
        var clientSecret = await service.GeneratePaymentTokenAsync(
            orderId,
            150m,
            "test@example.com",
            "John",
            "Doe",
            "+201000000000",
            CancellationToken.None);

        // Assert
        clientSecret.Should().Be("secret_token_12345");
    }

    [Fact]
    public void VerifyHmac_ValidSignature_ShouldReturnTrue()
    {
        // Arrange
        var hmacSecret = "my_secret_key";
        var payload = "order_123_paid";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(hmacSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var validHmac = Convert.ToHexString(hash).ToLower();

        var httpClient = new HttpClient();
        var options = Microsoft.Extensions.Options.Options.Create(new PaymobOptions
        {
            ApiKey = "key",
            IntegrationId = "123",
            HmacSecret = hmacSecret
        });

        var service = new PaymobService(httpClient, options);

        // Act & Assert
        var isValid = service.VerifyHmac(payload, validHmac);
        isValid.Should().BeTrue();

        var isInvalid = service.VerifyHmac(payload, "wrong_hmac_signature");
        isInvalid.Should().BeFalse();
    }
}
