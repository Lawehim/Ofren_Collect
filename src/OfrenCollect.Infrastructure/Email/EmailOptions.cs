namespace OfrenCollect.Infrastructure.Email;

/// <summary>
/// Email configuration. Disabled by default. Uses Brevo's transactional email HTTP API (port 443,
/// so it works on hosts that block outbound SMTP). The <see cref="ApiKey"/> is a secret and comes
/// from user-secrets/environment (§9); <see cref="AppBaseUrl"/> is the SPA origin used in links.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; init; }

    /// <summary>Brevo API key (the "API keys" tab, not the SMTP key). Sent as the <c>api-key</c> header.</summary>
    public string ApiKey { get; init; } = string.Empty;

    public string ApiBaseUrl { get; init; } = "https://api.brevo.com";

    /// <summary>Must be a sender verified in Brevo, or sends are rejected.</summary>
    public string FromAddress { get; init; } = string.Empty;

    public string FromName { get; init; } = "Ofren Collect";

    public string AppBaseUrl { get; init; } = string.Empty;
}
