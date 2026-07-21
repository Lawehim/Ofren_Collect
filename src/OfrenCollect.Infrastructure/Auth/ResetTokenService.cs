using System.Security.Cryptography;
using System.Text;
using OfrenCollect.Application.Abstractions;

namespace OfrenCollect.Infrastructure.Auth;

/// <summary>
/// Generates a cryptographically-random reset token and hashes it with SHA-256 for storage. The
/// token has 256 bits of entropy, so a fast hash is enough to make the stored value useless to an
/// attacker who reads the database (§9) — no slow KDF needed as for passwords.
/// </summary>
public sealed class ResetTokenService : IResetTokenService
{
    private const int TokenBytes = 32;

    public ResetToken Create()
    {
        var raw = Base64Url(RandomNumberGenerator.GetBytes(TokenBytes));
        return new ResetToken(raw, Hash(raw));
    }

    public string Hash(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
