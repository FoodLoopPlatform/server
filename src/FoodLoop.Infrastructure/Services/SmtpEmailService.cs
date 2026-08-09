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

    private const string LoginUrl = "https://web-nine-ivory-36.vercel.app/login";

    public bool IsDevStub => false;

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken cancellationToken = default)
    {
        var subject = "FoodLoop - Reset Your Password";
        var body = $"Hello,\n\nUse this token to reset your password:\n\n{resetToken}\n\nThis token expires shortly. If you did not request a password reset, ignore this email.\n\nOnce reset, you can log in to your account here:\n{LoginUrl}\n\nThank you,\nFoodLoop Team";
        await SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string fullName, CancellationToken cancellationToken = default)
    {
        var subject = "Welcome to FoodLoop!";
        var body = $"Hello {fullName},\n\nWelcome to FoodLoop! Your account has been registered successfully.\n\nYou can log in to your account here:\n{LoginUrl}\n\nThank you,\nFoodLoop Team";
        await SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public async Task SendPendingReviewEmailAsync(string toEmail, string fullName, string organizationName, CancellationToken cancellationToken = default)
    {
        var subject = "FoodLoop - Your Application Is Under Review";
        var body = $"Hello {fullName},\n\nThank you for registering \"{organizationName}\" on FoodLoop!\n\nYour application is currently under review by our team. You will receive an email once a decision has been made.\n\nIn the meantime, you can upload your verification documents by logging into your account here:\n{LoginUrl}\n\nThank you,\nFoodLoop Team";
        await SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public async Task SendApprovalEmailAsync(string toEmail, string fullName, string organizationName, CancellationToken cancellationToken = default)
    {
        var subject = "FoodLoop - Your Account Has Been Approved!";
        var body = $"Hello {fullName},\n\nCongratulations! Your organization \"{organizationName}\" has been verified and approved on FoodLoop.\n\nYou can now log in and start using all merchant features here:\n{LoginUrl}\n\nThank you,\nFoodLoop Team";
        await SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public async Task SendRejectionEmailAsync(string toEmail, string fullName, string organizationName, string? adminNote, CancellationToken cancellationToken = default)
    {
        var noteSection = string.IsNullOrWhiteSpace(adminNote) ? string.Empty : $"\n\nAdmin note: {adminNote}";
        var subject = "FoodLoop - Account Verification Update";
        var body = $"Hello {fullName},\n\nWe have reviewed your application for \"{organizationName}\" and unfortunately it was not approved at this time.{noteSection}\n\nPlease review your submitted documents and resubmit by logging into your account here:\n{LoginUrl}\n\nIf you have questions, contact our support team.\n\nThank you,\nFoodLoop Team";
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
