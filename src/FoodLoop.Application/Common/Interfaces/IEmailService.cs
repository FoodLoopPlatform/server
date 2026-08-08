namespace FoodLoop.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the external Email Service integration.
/// Three implementations ship:
///   - BrevoEmailService  (production — Brevo HTTP API, no IP whitelist)
///   - SmtpEmailService   (fallback — MailKit SMTP)
///   - NullEmailService   (dev stub — logs instead of sending, exposes debug tokens)
/// </summary>
public interface IEmailService
{
    /// <summary>True when the implementation is a dev/test stub that does not actually send emails.</summary>
    bool IsDevStub { get; }

    /// <summary>Sends a welcome email immediately after a new account is registered.</summary>
    Task SendWelcomeEmailAsync(string toEmail, string fullName, CancellationToken cancellationToken = default);

    /// <summary>Sends a password reset token to the user.</summary>
    Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies a merchant or charity that their account/organization was approved by an admin.
    /// Sent from VerifyOrganizationCommandHandler after status is set to Verified.
    /// </summary>
    Task SendApprovalEmailAsync(string toEmail, string fullName, string organizationName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies a merchant or charity that their account/organization was rejected by an admin.
    /// Sent from VerifyOrganizationCommandHandler after status is set to Rejected.
    /// Includes the admin note so the user knows what to fix.
    /// </summary>
    Task SendRejectionEmailAsync(string toEmail, string fullName, string organizationName, string? adminNote, CancellationToken cancellationToken = default);
}
