using FoodLoop.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsDevStub => false;

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken cancellationToken = default)
    {
        var subject = "FoodLoop - Reset Your Password";
        var body = $"Hello,\n\nUse this token to reset your password: {resetToken}\n\nThank you,\nFoodLoop Team";
        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string fullName, CancellationToken cancellationToken = default)
    {
        var subject = "Welcome to FoodLoop!";
        var body = $"Hello {fullName},\n\nWelcome to FoodLoop! Your account has been registered successfully.\n\nThank you,\nFoodLoop Team";
        await SendEmailAsync(toEmail, subject, body);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var host = _configuration["SMTP_HOST"] ?? Environment.GetEnvironmentVariable("SMTP_HOST");
        var portStr = _configuration["SMTP_PORT"] ?? Environment.GetEnvironmentVariable("SMTP_PORT");
        var username = _configuration["SMTP_USERNAME"] ?? Environment.GetEnvironmentVariable("SMTP_USERNAME");
        var password = _configuration["SMTP_PASSWORD"] ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD");
        var fromEmail = _configuration["SMTP_FROM_EMAIL"] ?? Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL") ?? "noreply@foodloop.com";

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            _logger.LogWarning("[SMTP EMAIL BYPASS] SMTP is not fully configured. Email was not sent. Host: {Host}, User: {User}", host, username);
            return;
        }

        int.TryParse(portStr, out var port);
        if (port == 0) port = 587;

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(username, password),
            EnableSsl = true
        };

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(fromEmail, "FoodLoop"),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        mailMessage.To.Add(toEmail);

        try
        {
            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Email sent successfully to {Email} with subject: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} via SMTP.", toEmail);
        }
    }
}
