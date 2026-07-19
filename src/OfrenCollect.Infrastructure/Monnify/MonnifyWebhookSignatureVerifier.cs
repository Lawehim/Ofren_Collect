using System.Security.Cryptography;
using System.Text;
using OfrenCollect.Application.Abstractions;

namespace OfrenCollect.Infrastructure.Monnify;

/// <summary>
/// Verifies Monnify's <c>monnify-signature</c> header: HMAC-SHA512 of the raw request body,
/// keyed with the client Secret Key, lowercase hex, compared in constant time. Fails closed.
/// </summary>
public sealed class MonnifyWebhookSignatureVerifier : IMonnifyWebhookVerifier
{
    private readonly MonnifyOptions _options;

    public MonnifyWebhookSignatureVerifier(MonnifyOptions options) => _options = options;

    public bool IsValid(string rawBody, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        var key = Encoding.UTF8.GetBytes(_options.SecretKey);
        var body = Encoding.UTF8.GetBytes(rawBody);
        var computedHex = Convert.ToHexStringLower(HMACSHA512.HashData(key, body));

        // Constant-time comparison over the hex strings (differing lengths return false).
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(signatureHeader.Trim()));
    }
}
