namespace FoodLoop.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the external Email Service integration (see System Architecture,
/// section 5). Sprint 1 ships a logging/no-op implementation; a real provider
/// (SendGrid/SES/etc.) can be swapped in later without touching Application code.
/// </summary>
public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken cancellationToken = default);
    Task SendWelcomeEmailAsync(string toEmail, string fullName, CancellationToken cancellationToken = default);
}
