using System.Net;
using OfrenCollect.Application.Abstractions;

namespace OfrenCollect.Infrastructure.Email;

/// <summary>
/// Renders account emails and hands them to the background queue — instantly, so registration and
/// password-reset requests never wait on the email provider. When email is disabled, nothing is
/// queued. The <see cref="EmailDispatcher"/> performs the actual (best-effort) send.
/// </summary>
public sealed class AccountEmailService : IAccountEmailService
{
    private const int ResetLinkLifetimeMinutes = 30;

    private readonly IEmailOutbox _outbox;
    private readonly EmailOptions _options;

    public AccountEmailService(IEmailOutbox outbox, EmailOptions options)
    {
        _outbox = outbox;
        _options = options;
    }

    public Task SendWelcomeAsync(string toEmail, string businessName, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return Task.CompletedTask;
        }

        var html =
            $"<p>Welcome to Ofren Collect, {WebUtility.HtmlEncode(businessName)}.</p>"
            + "<p>Your account is ready — sign in to start collecting and reconciling payments automatically.</p>";
        _outbox.Enqueue(new QueuedEmail(toEmail, "Welcome to Ofren Collect", html));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string rawToken, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return Task.CompletedTask;
        }

        var link = $"{_options.AppBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(rawToken)}";
        var html =
            "<p>We received a request to reset your Ofren Collect password.</p>"
            + $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Reset your password</a></p>"
            + $"<p>This link expires in {ResetLinkLifetimeMinutes} minutes. If you didn't request it, ignore this email.</p>";
        _outbox.Enqueue(new QueuedEmail(toEmail, "Reset your Ofren Collect password", html));
        return Task.CompletedTask;
    }
}
