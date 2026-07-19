using System.Globalization;
using System.Security.Cryptography;
using OfrenCollect.Application.Abstractions;

namespace OfrenCollect.Infrastructure.Auth;

/// <summary>
/// Password hasher using PBKDF2 (SHA-256). The stored value is
/// <c>{iterations}.{base64 salt}.{base64 hash}</c>, so a hash carries the parameters needed
/// to verify it. Verification is constant-time.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private const char Separator = '.';
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);

        return string.Join(
            Separator,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key));
    }

    public bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split(Separator);
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] key;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            key = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var attempt = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, key.Length);
        return CryptographicOperations.FixedTimeEquals(attempt, key);
    }
}
