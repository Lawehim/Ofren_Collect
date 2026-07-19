namespace OfrenCollect.Infrastructure.Auth;

/// <summary>
/// Strongly-typed JWT configuration, bound from the "Jwt" section. The SigningKey is a secret
/// and comes from user-secrets or environment only (CLAUDE.md §9, NFR-1.8).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 60;
}
