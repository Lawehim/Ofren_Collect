using OfrenCollect.Domain.Users;

namespace OfrenCollect.Application.Abstractions.Persistence;

/// <summary>Reads and writes users. Email lookups are pre-auth, so they cross tenants.</summary>
public interface IUserRepository
{
    /// <summary>Whether an account already exists for this (normalised) email.</summary>
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Finds a user by email for login. There is no ambient tenant yet, so the implementation
    /// bypasses the global tenant filter; email is globally unique, so this resolves one user.
    /// </summary>
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Finds a user by the hash of their outstanding reset token (the password-reset path is
    /// pre-auth, so it bypasses the tenant filter). Returns null if no user holds that hash.
    /// </summary>
    Task<User?> FindByResetTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    void Add(User user);
}
