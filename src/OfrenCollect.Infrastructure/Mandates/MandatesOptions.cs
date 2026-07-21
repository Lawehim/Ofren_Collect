namespace OfrenCollect.Infrastructure.Mandates;

/// <summary>
/// Feature flag for direct-debit mandates (FR-9). Disabled by default so the capability stays dark
/// until its end-to-end flow is confirmed against the sandbox; flipping it is a config change (§6).
/// </summary>
public sealed class MandatesOptions
{
    public const string SectionName = "Mandates";

    public bool Enabled { get; init; }
}
