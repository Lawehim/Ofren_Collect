namespace OfrenCollect.Infrastructure.Refunds;

/// <summary>
/// Feature flag for the refund capability (FR-11). Disabled by default so the feature stays dark
/// until its end-to-end path is confirmed against the sandbox; flipping it is a config change, not
/// a code edit (§6).
/// </summary>
public sealed class RefundsOptions
{
    public const string SectionName = "Refunds";

    public bool Enabled { get; init; }
}
