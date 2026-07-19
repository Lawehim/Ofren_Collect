using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.Users;

/// <summary>
/// A login belonging to exactly one tenant. The password is only ever held as a hash
/// (CLAUDE.md §8); the email is normalised to lower case for consistent lookups.
/// </summary>
public sealed class User : AggregateRoot
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

    public static User Create(Guid tenantId, string email, string passwordHash, UserRole role)
    {
        Guard.AgainstNullOrWhiteSpace(email, nameof(email));
        Guard.AgainstNullOrWhiteSpace(passwordHash, nameof(passwordHash));

        return new User(Guid.NewGuid(), tenantId, email.Trim().ToLowerInvariant(), passwordHash, role);
    }
}
