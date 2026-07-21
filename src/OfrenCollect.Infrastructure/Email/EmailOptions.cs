namespace OfrenCollect.Infrastructure.Email;

/// <summary>
/// Email/SMTP configuration. Disabled by default; when enabled it points at any SMTP provider
/// (e.g. Brevo's smtp-relay.brevo.com). Username/Password are secrets and come from
/// user-secrets/environment (§9). <see cref="AppBaseUrl"/> is the SPA origin used to build links.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; init; }

    public string SmtpHost { get; init; } = string.Empty;

    public int SmtpPort { get; init; } = 587;

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string FromAddress { get; init; } = string.Empty;

    public string FromName { get; init; } = "Ofren Collect";

    public string AppBaseUrl { get; init; } = string.Empty;
}
