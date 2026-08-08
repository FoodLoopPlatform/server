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

    public Task SendWelcomeEmailAsync(string toEmail, string fullName, CancellationToken cancellationToken = default)
    {
        var subject = "Welcome to FoodLoop!";
        var body = $"Hello {fullName},\n\nWelcome to FoodLoop! Your account has been registered successfully.\n\nThank you,\nFoodLoop Team";
        return SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken cancellationToken = default)
    {
        var subject = "FoodLoop - Reset Your Password";
        var body = $"Hello,\n\nUse this token to reset your password:\n\n{resetToken}\n\nThis token expires shortly. If you did not request a password reset, ignore this email.\n\nThank you,\nFoodLoop Team";
        return SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public Task SendPendingReviewEmailAsync(string toEmail, string fullName, string organizationName, CancellationToken cancellationToken = default)
    {
        var subject = "FoodLoop - Your Application Is Under Review";
        var body = $"Hello {fullName},\n\nThank you for registering \"{organizationName}\" on FoodLoop!\n\nYour application is currently under review by our team. You will receive an email once a decision has been made.\n\nIn the meantime, you can upload your verification documents by logging into your account.\n\nThank you,\nFoodLoop Team";
        return SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public Task SendApprovalEmailAsync(string toEmail, string fullName, string organizationName, CancellationToken cancellationToken = default)
    {
        var subject = "FoodLoop - Your Account Has Been Approved!";
        var body = $"Hello {fullName},\n\nCongratulations! Your organization \"{organizationName}\" has been verified and approved on FoodLoop.\n\nYou can now log in and start using all merchant features.\n\nThank you,\nFoodLoop Team";
        return SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public Task SendRejectionEmailAsync(string toEmail, string fullName, string organizationName, string? adminNote, CancellationToken cancellationToken = default)
    {
        var noteSection = string.IsNullOrWhiteSpace(adminNote) ? string.Empty : $"\n\nAdmin note: {adminNote}";
        var subject = "FoodLoop - Account Verification Update";
        var body = $"Hello {fullName},\n\nWe have reviewed your application for \"{organizationName}\" and unfortunately it was not approved at this time.{noteSection}\n\nPlease review your submitted documents and resubmit. If you have questions, contact our support team.\n\nThank you,\nFoodLoop Team";
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

            // Use per-request headers to avoid thread-safety issues with DefaultRequestHeaders
            using var request = new HttpRequestMessage(HttpMethod.Post, BrevoApiUrl);
            request.Headers.Add("api-key", _apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = content;

            var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email sent to {Email}: {Subject}", toEmail, subject);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Brevo API {Status} for {Email}: {Error}", (int)response.StatusCode, toEmail, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
        }
    }
}
