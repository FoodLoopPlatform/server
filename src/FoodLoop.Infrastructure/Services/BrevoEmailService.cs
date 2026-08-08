using FoodLoop.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Services;

/// <summary>
/// Sends transactional emails via Brevo's HTTP API (v3).
/// Uses an API key — no IP whitelist required, works from any host.
/// </summary>
public class BrevoEmailService : IEmailService
{
    private const string BrevoApiUrl = "https://api.brevo.com/v3/smtp/email";

    private readonly string _apiKey;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly ILogger<BrevoEmailService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public BrevoEmailService(
        string apiKey,
        string fromEmail,
        string fromName,
        IHttpClientFactory httpClientFactory,
        ILogger<BrevoEmailService> logger)
    {
        _apiKey = apiKey;
        _fromEmail = fromEmail;
        _fromName = fromName;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsDevStub => false;

    public Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken cancellationToken = default)
    {
        var subject = "FoodLoop - Reset Your Password";
        var body = $"Hello,\n\nUse this token to reset your password:\n\n{resetToken}\n\nThis token expires shortly. If you did not request a password reset, ignore this email.\n\nThank you,\nFoodLoop Team";
        return SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public Task SendWelcomeEmailAsync(string toEmail, string fullName, CancellationToken cancellationToken = default)
    {
        var subject = "Welcome to FoodLoop!";
        var body = $"Hello {fullName},\n\nWelcome to FoodLoop! Your account has been registered successfully.\n\nThank you,\nFoodLoop Team";
        return SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        try
        {
            var payload = new
            {
                sender = new { email = _fromEmail, name = _fromName },
                to = new[] { new { email = toEmail } },
                subject,
                textContent = body
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var client = _httpClientFactory.CreateClient("brevo");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("api-key", _apiKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.PostAsync(BrevoApiUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email sent successfully to {Email} via Brevo API", toEmail);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Brevo API returned {Status} sending to {Email}: {Error}",
                    (int)response.StatusCode, toEmail, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} via Brevo API", toEmail);
        }
    }
}
