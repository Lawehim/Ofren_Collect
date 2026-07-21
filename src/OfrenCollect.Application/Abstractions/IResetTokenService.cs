namespace OfrenCollect.Application.Abstractions;

/// <summary>
/// Creates and hashes password-reset tokens. The raw token is emailed to the user; only its hash is
/// stored, so a database leak never exposes usable reset tokens (§9). The token is high-entropy, so
/// a fast hash is sufficient (unlike passwords, which need a slow KDF).
/// </summary>
public interface IResetTokenService
{
    ResetToken Create();

    /// <summary>Hashes a raw token the same way <see cref="Create"/> does, for lookup on reset.</summary>
    string Hash(string rawToken);
}

/// <summary>A reset token: the raw value to email, and the hash to store.</summary>
public sealed record ResetToken(string RawToken, string HashedToken);
