using OfrenCollect.Domain.Abstractions;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.Users;

/// <summary>
/// A login belonging to exactly one tenant. The password is only ever held as a hash
/// (CLAUDE.md §8); the email is normalised to lower case for consistent lookups.
/// </summary>
public sealed class User : AggregateRoot, ITenantOwned
{
    private User()
    {
    }

    private User(Guid id, Guid tenantId, string email, string passwordHash, UserRole role)
        : base(id)
    {
        TenantId = tenantId;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
    }

    public Guid TenantId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    /// <summary>Hash of the active password-reset token; null when none is outstanding (§9).</summary>
    public string? PasswordResetTokenHash { get; private set; }

    public DateTimeOffset? PasswordResetTokenExpiresAt { get; private set; }

    public static User Create(Guid tenantId, string email, string passwordHash, UserRole role)
    {
        Guard.AgainstNullOrWhiteSpace(email, nameof(email));
        Guard.AgainstNullOrWhiteSpace(passwordHash, nameof(passwordHash));

        return new User(Guid.NewGuid(), tenantId, email.Trim().ToLowerInvariant(), passwordHash, role);
    }

    /// <summary>Records the hash of a freshly issued reset token and when it expires.</summary>
    public void SetPasswordResetToken(string tokenHash, DateTimeOffset expiresAt)
    {
        Guard.AgainstNullOrWhiteSpace(tokenHash, nameof(tokenHash));
        PasswordResetTokenHash = tokenHash;
        PasswordResetTokenExpiresAt = expiresAt;
    }

    /// <summary>Whether an outstanding reset token exists and has not yet expired.</summary>
    public bool IsResetTokenValid(DateTimeOffset now) =>
        PasswordResetTokenHash is not null && PasswordResetTokenExpiresAt is { } expiry && now < expiry;

    /// <summary>Sets a new password hash and clears the reset token, so the link is single-use.</summary>
    public void ResetPassword(string newPasswordHash)
    {
        Guard.AgainstNullOrWhiteSpace(newPasswordHash, nameof(newPasswordHash));
        PasswordHash = newPasswordHash;
        PasswordResetTokenHash = null;
        PasswordResetTokenExpiresAt = null;
    }
}
