namespace OfrenCollect.Infrastructure.Monnify;

/// <summary>
/// Strongly-typed Monnify configuration, bound from the "Monnify" section. Secrets
/// (ApiKey/SecretKey/ContractCode) come from user-secrets or environment only (CLAUDE.md §9).
/// </summary>
public sealed class MonnifyOptions
{
    public const string SectionName = "Monnify";

    public string BaseUrl { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public string SecretKey { get; init; } = string.Empty;

    public string ContractCode { get; init; } = string.Empty;

    /// <summary>
    /// Whether to enforce the monnify-signature header on webhooks. Monnify may send it only in
    /// production; in sandbox this can be false, relying on mandatory server-side verification.
    /// Defaults to true (fail-closed for production).
    /// </summary>
    public bool VerifyWebhookSignature { get; init; } = true;
}
