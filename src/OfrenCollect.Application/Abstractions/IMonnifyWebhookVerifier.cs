namespace OfrenCollect.Application.Abstractions;

/// <summary>
/// Verifies the authenticity of an incoming Monnify webhook before its contents are trusted
/// (FR-3.3, NFR-1.4). Fails closed: a missing or invalid signature is never valid.
/// </summary>
public interface IMonnifyWebhookVerifier
{
    /// <summary>
    /// Whether <paramref name="signatureHeader"/> is the correct signature for the exact raw
    /// request body. Verification is over the raw bytes as received — never a re-serialised body.
    /// </summary>
    bool IsValid(string rawBody, string? signatureHeader);
}
