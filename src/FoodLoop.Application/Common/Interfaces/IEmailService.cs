namespace FoodLoop.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the external Email Service integration.
/// Three implementations ship:
///   - BrevoEmailService  (production — Brevo HTTP API, no IP whitelist)
///   - SmtpEmailService   (fallback — MailKit SMTP)
///   - NullEmailService   (dev stub — logs instead of sending, exposes debug tokens)
///
/// Email triggers:
///   POST /auth/register              -> SendWelcomeEmailAsync (Customer)
///                                    -> SendPendingReviewEmailAsync (Merchant/Charity)
///   POST /auth/resend-verification   -> SendWelcomeEmailAsync (Customer)
///                                    -> SendPendingReviewEmailAsync (Merchant/Charity)
///   POST /auth/forgot-password       -> SendPasswordResetEmailAsync
///   PATCH /admin/stores/{id}/verify  -> SendApprovalEmailAsync or SendRejectionEmailAsync
///   PATCH /admin/charities/{id}/verify -> SendApprovalEmailAsync or SendRejectionEmailAsync
/// </summary>
public interface IEmailService
{
    /// <summary>True when the implementation is a dev/test stub that does not actually send emails.</summary>
    bool IsDevStub { get; }

    /// <summary>
    /// Sends a welcome email to a Customer immediately after registration.
    /// Customers are active immediately so this is a simple welcome.
    /// </summary>
    Task SendWelcomeEmailAsync(string toEmail, string fullName, CancellationToken cancellationToken = default);

    /// <summary>Sends a password reset token to the user.</summary>
    Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies a Merchant or Charity that their registration was received and is under review.
    /// Sent after Merchant/Charity registration and on resend-verification.
    /// Their account stays PendingVerification until admin approves.
    /// </summary>
    Task SendPendingReviewEmailAsync(string toEmail, string fullName, string organizationName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies a Merchant or Charity that their account was approved by an admin.
    /// Sent from VerifyOrganizationCommandHandler when action = Approved.
    /// </summary>
    Task SendApprovalEmailAsync(string toEmail, string fullName, string organizationName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies a Merchant or Charity that their account was rejected by an admin.
    /// Includes the admin note so the user knows what to correct and resubmit.
    /// Sent from VerifyOrganizationCommandHandler when action = Rejected.
    /// </summary>
    Task SendRejectionEmailAsync(string toEmail, string fullName, string organizationName, string? adminNote, CancellationToken cancellationToken = default);
}
