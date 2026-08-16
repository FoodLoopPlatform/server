using FoodLoop.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _config;

    public PaymobService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
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
        var apiKey = _config["Paymob:ApiKey"] ?? throw new InvalidOperationException("Paymob API Key is not configured.");
        var integrationIdStr = _config["Paymob:IntegrationId"] ?? throw new InvalidOperationException("Paymob Integration ID is not configured.");
        if (!int.TryParse(integrationIdStr, out var integrationId))
        {
            throw new InvalidOperationException("Paymob Integration ID must be a valid integer.");
        }

        // Base URL: Use accept-alpha.paymob.com for sandbox/test environment
        var baseUrl = _config["Paymob:BaseUrl"] ?? "https://accept-alpha.paymob.com";

        // Step 1: Authenticate
        var authResponse = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/auth/tokens", new { api_key = apiKey }, cancellationToken);
        if (!authResponse.IsSuccessStatusCode)
        {
            var err = await authResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Paymob Auth failed: {err}");
        }
        var authResult = await authResponse.Content.ReadFromJsonAsync<PaymobAuthResponse>(cancellationToken: cancellationToken);
        var token = authResult?.token ?? throw new InvalidOperationException("Paymob did not return auth token.");

        // Step 2: Register Order
        var amountCents = (int)(amount * 100);
        var orderResponse = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/ecommerce/orders", new
        {
            auth_token = token,
            delivery_needed = "false",
            amount_cents = amountCents,
            currency = "EGP",
            items = Array.Empty<object>()
        }, cancellationToken);

        if (!orderResponse.IsSuccessStatusCode)
        {
            var err = await orderResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Paymob Order registration failed: {err}");
        }
        var orderResult = await orderResponse.Content.ReadFromJsonAsync<PaymobOrderResponse>(cancellationToken: cancellationToken);
        var paymobOrderId = orderResult?.id ?? throw new InvalidOperationException("Paymob did not return order ID.");

        // Step 3: Payment Key Generation
        var paymentKeyResponse = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/acceptance/payment_keys", new
        {
            auth_token = token,
            amount_cents = amountCents,
            expiration = 3600, // 1 hour
            order_id = paymobOrderId.ToString(),
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
            currency = "EGP",
            integration_id = integrationId,
            lock_order_when_paid = "true"
        }, cancellationToken);

        if (!paymentKeyResponse.IsSuccessStatusCode)
        {
            var err = await paymentKeyResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Paymob Payment key generation failed: {err}");
        }
        var paymentKeyResult = await paymentKeyResponse.Content.ReadFromJsonAsync<PaymobPaymentKeyResponse>(cancellationToken: cancellationToken);
        return paymentKeyResult?.token ?? throw new InvalidOperationException("Paymob did not return payment token.");
    }

    public bool VerifyHmac(string payload, string hmacReceived)
    {
        var hmacSecret = _config["Paymob:HmacSecret"];
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

public class PaymobAuthResponse
{
    public string token { get; set; } = string.Empty;
}

public class PaymobOrderResponse
{
    public long id { get; set; }
}

public class PaymobPaymentKeyResponse
{
    public string token { get; set; } = string.Empty;
}
