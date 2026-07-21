using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using OfrenCollect.Application.Abstractions;

namespace OfrenCollect.Infrastructure.Email;

/// <summary>
/// Sends account emails over SMTP (MailKit). Best-effort and feature-flagged: when disabled it logs
/// and no-ops; a send failure is logged but never thrown, so it cannot break registration or a reset
/// request, nor reveal whether an account exists (§9, §10 — this catch recovers meaningfully).
/// </summary>
public sealed class AccountEmailService : IAccountEmailService
{
    private const int ResetLinkLifetimeMinutes = 30;

    private readonly EmailOptions _options;
    private readonly ILogger<AccountEmailService> _logger;

    public AccountEmailService(EmailOptions options, ILogger<AccountEmailService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task SendWelcomeAsync(string toEmail, string businessName, CancellationToken cancellationToken)
    {
        var html =
            $"<p>Welcome to Ofren Collect, {WebUtility.HtmlEncode(businessName)}.</p>"
            + "<p>Your account is ready — sign in to start collecting and reconciling payments automatically.</p>";
        return SendAsync(toEmail, "Welcome to Ofren Collect", html, cancellationToken);
    }

    public Task SendPasswordResetAsync(string toEmail, string rawToken, CancellationToken cancellationToken)
    {
        var link = $"{_options.AppBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(rawToken)}";
        var html =
            "<p>We received a request to reset your Ofren Collect password.</p>"
            + $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Reset your password</a></p>"
            + $"<p>This link expires in {ResetLinkLifetimeMinutes} minutes. If you didn't request it, ignore this email.</p>";
        return SendAsync(toEmail, "Reset your Ofren Collect password", html, cancellationToken);
    }

    private async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Email disabled — skipping '{Subject}' to {Recipient}.", subject, toEmail);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, SecureSocketOptions.StartTls, cancellationToken);
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Failed to send '{Subject}' to {Recipient}.", subject, toEmail);
        }
    }
}
