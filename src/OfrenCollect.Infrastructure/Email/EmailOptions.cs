namespace OfrenCollect.Infrastructure.Email;

/// <summary>
/// Email configuration. Disabled by default. Sends over an HTTP API (port 443, so it works where
/// outbound SMTP is blocked, e.g. Render). <see cref="ApiKey"/> is a secret (§9);
/// <see cref="AppBaseUrl"/> is the SPA origin used in links.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; init; }

    /// <summary>Which provider to send through (Brevo for real delivery, Mailtrap for a sandbox inbox).</summary>
    public EmailProvider Provider { get; init; } = EmailProvider.Brevo;

    /// <summary>Brevo: the "API keys" tab key. Mailtrap: the API token. Sent as the provider's auth header.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Mailtrap Sandbox only: the testing inbox id (from the inbox's "Integrations → API" tab).</summary>
    public string MailtrapInboxId { get; init; } = string.Empty;

    /// <summary>Brevo only: must be a sender verified in Brevo, or sends are rejected.</summary>
    public string FromAddress { get; init; } = string.Empty;

    public string FromName { get; init; } = "Ofren Collect";

    public string AppBaseUrl { get; init; } = string.Empty;
}
