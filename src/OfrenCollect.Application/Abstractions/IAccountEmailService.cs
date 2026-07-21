namespace OfrenCollect.Application.Abstractions;

/// <summary>
/// Sends account-related emails (welcome, password reset). A boundary (§2.3) so handlers stay free
/// of SMTP/templating detail; the implementation is best-effort and feature-flagged, so a disabled
/// or failing mail provider never breaks the calling flow.
/// </summary>
public interface IAccountEmailService
{
    Task SendWelcomeAsync(string toEmail, string businessName, CancellationToken cancellationToken);

    /// <summary>Emails a password-reset link containing the raw (un-hashed) token.</summary>
    Task SendPasswordResetAsync(string toEmail, string rawToken, CancellationToken cancellationToken);
}
