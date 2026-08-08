using FoodLoop.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace FoodLoop.Infrastructure.Services;

/// <summary>
/// Sprint 1 placeholder: logs the email instead of sending it, so the Auth flows
/// (forgot/reset password, welcome email) work end-to-end before a real provider
/// (SendGrid/SES/etc.) is wired in per the System Architecture "Email Service" integration.
/// </summary>
public class NullEmailService : IEmailService
{
    private readonly ILogger<NullEmailService> _logger;

    public NullEmailService(ILogger<NullEmailService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsDevStub => true;

    public Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[DEV EMAIL] Password reset for {Email}. Token: {Token}", toEmail, resetToken);
        return Task.CompletedTask;
    }

    public Task SendWelcomeEmailAsync(string toEmail, string fullName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[DEV EMAIL] Welcome {Name} <{Email}>", fullName, toEmail);
        return Task.CompletedTask;
    }

    public Task SendApprovalEmailAsync(string toEmail, string fullName, string organizationName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[DEV EMAIL] Approval: {Name} <{Email}> — Organization: {Org}", fullName, toEmail, organizationName);
        return Task.CompletedTask;
    }

    public Task SendRejectionEmailAsync(string toEmail, string fullName, string organizationName, string? adminNote, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[DEV EMAIL] Rejection: {Name} <{Email}> — Organization: {Org} — Note: {Note}", fullName, toEmail, organizationName, adminNote);
        return Task.CompletedTask;
    }
}
