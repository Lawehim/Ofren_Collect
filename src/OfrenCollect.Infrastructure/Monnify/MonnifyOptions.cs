namespace OfrenCollect.Infrastructure.Monnify;

/// <summary>
/// Strongly-typed Monnify configuration, bound from the "Monnify" section. Secrets
/// (ApiKey/SecretKey/ContractCode) come from user-secrets or environment only (CLAUDE.md §9).
/// </summary>
public sealed class MonnifyOptions
{
    public const string SectionName = "Monnify";

    public string BaseUrl { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public string SecretKey { get; init; } = string.Empty;

    public string ContractCode { get; init; } = string.Empty;
}
