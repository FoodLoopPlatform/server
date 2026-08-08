using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<SmtpOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
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
        if (string.IsNullOrEmpty(_options.Host) || string.IsNullOrEmpty(_options.Username) || string.IsNullOrEmpty(_options.Password))
        {
            _logger.LogWarning("[SMTP EMAIL BYPASS] SMTP is not fully configured. Email was not sent. Host: {Host}, User: {User}", _options.Host, _options.Username);
            return;
        }

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            UseDefaultCredentials = false,   // must be false before setting Credentials
            Credentials = new NetworkCredential(_options.Username, _options.Password),
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, "FoodLoop"),
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
