namespace OfrenCollect.Domain.Users;

/// <summary>
/// A user's role within their tenant. Explicit non-zero values so an unset default
/// never silently grants Owner privileges.
/// </summary>
public enum UserRole
{
    Owner = 1,
    Staff = 2
}
