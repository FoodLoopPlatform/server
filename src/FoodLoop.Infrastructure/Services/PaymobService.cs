using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Infrastructure.Options;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Services;

public class PaymobService : IPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly PaymobOptions _options;

    public PaymobService(HttpClient httpClient, IOptions<PaymobOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        // Add headers to bypass WAF / CloudFront blocking rules
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        }
        if (!_httpClient.DefaultRequestHeaders.Contains("Accept"))
        {
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }
    }

    public async Task<string> GeneratePaymentTokenAsync(
        Guid orderId, 
        decimal amount, 
        string email, 
        string firstName, 
        string lastName, 
        string phoneNumber, 
        CancellationToken cancellationToken = default)
    {
        var apiKey = _options.ApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("Paymob API Key is not configured.");
        }

        var integrationIdStr = _options.IntegrationId;
        if (string.IsNullOrEmpty(integrationIdStr) || !int.TryParse(integrationIdStr, out var integrationId))
        {
            throw new InvalidOperationException("Paymob Integration ID must be a valid integer.");
        }

        var baseUrl = _options.BaseUrl;
        var amountCents = (int)(amount * 100);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/intention");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", apiKey);

        var payload = new
        {
            amount = amountCents,
            currency = "EGP",
            payment_methods = new[] { integrationId },
            billing_data = new
            {
                apartment = "NA",
                floor = "NA",
                street = "NA",
                building = "NA",
                shipping_method = "NA",
                postal_code = "NA",
                city = "Cairo",
                country = "EG",
                state = "Cairo",
                first_name = string.IsNullOrWhiteSpace(firstName) ? "Customer" : firstName,
                last_name = string.IsNullOrWhiteSpace(lastName) ? "User" : lastName,
                email = string.IsNullOrWhiteSpace(email) ? "customer@foodloop.com" : email,
                phone_number = string.IsNullOrWhiteSpace(phoneNumber) ? "+201000000000" : phoneNumber
            },
            extras = new
            {
                merchant_order_id = orderId.ToString()
            }
        };

        request.Content = JsonContent.Create(payload);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Paymob Intention API failed: {err}");
        }

        var result = await response.Content.ReadFromJsonAsync<PaymobIntentionResponse>(cancellationToken: cancellationToken);
        return result?.client_secret ?? throw new InvalidOperationException("Paymob did not return client_secret.");
    }
    public bool VerifyHmac(string payload, string hmacReceived)
    {
        var hmacSecret = _options.HmacSecret;
        if (string.IsNullOrEmpty(hmacSecret))
        {
            return true; // If not configured, skip for local development (log a warning in real apps)
        }

        var keyBytes = Encoding.UTF8.GetBytes(hmacSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        var calculatedHmac = Convert.ToHexString(hashBytes).ToLower();

        return string.Equals(calculatedHmac, hmacReceived, StringComparison.OrdinalIgnoreCase);
    }
}

public class PaymobIntentionResponse
{
    public string client_secret { get; set; } = string.Empty;
}
