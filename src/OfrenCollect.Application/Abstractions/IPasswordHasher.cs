namespace OfrenCollect.Application.Abstractions;

/// <summary>
/// Hashes and verifies passwords with a strong KDF. Passwords are only ever stored as a hash
/// (CLAUDE.md §8); the plaintext is never persisted or logged.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Produces a salted hash string that embeds the parameters needed to verify it.</summary>
    string Hash(string password);

    /// <summary>Verifies a candidate password against a previously produced hash, in constant time.</summary>
    bool Verify(string password, string passwordHash);
}
