using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Infrastructure.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Services;

/// <summary>
/// Sends transactional emails via SMTP using MailKit.
/// MailKit handles STARTTLS and AUTH LOGIN correctly for all relay providers (Brevo, SendGrid, etc.)
/// unlike System.Net.Mail.SmtpClient which has known auth issues on .NET Core.
/// </summary>
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
        var body = $"Hello,\n\nUse this token to reset your password:\n\n{resetToken}\n\nThis token expires shortly. If you did not request a password reset, ignore this email.\n\nThank you,\nFoodLoop Team";
        await SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string fullName, CancellationToken cancellationToken = default)
    {
        var subject = "Welcome to FoodLoop!";
        var body = $"Hello {fullName},\n\nWelcome to FoodLoop! Your account has been registered successfully.\n\nThank you,\nFoodLoop Team";
        await SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.Host) ||
            string.IsNullOrEmpty(_options.Username) ||
            string.IsNullOrEmpty(_options.Password))
        {
            _logger.LogWarning("[SMTP EMAIL BYPASS] SMTP is not fully configured. Email was not sent. Host={Host} User={User}",
                _options.Host, _options.Username);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("FoodLoop", _options.FromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();

            // Connect with STARTTLS on port 587 (AUTO picks the right TLS mode)
            await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.StartTls, cancellationToken);

            // Authenticate — MailKit always sends AUTH before any mail commands
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            _logger.LogInformation("Email sent successfully to {Email} via {Host}", toEmail, _options.Host);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} via {Host}", toEmail, _options.Host);
        }
    }
}
