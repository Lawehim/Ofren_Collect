namespace OfrenCollect.Infrastructure.Jobs;

/// <summary>
/// Settings for the keep-warm pinger (prevents a Render free instance spinning down). When
/// <see cref="Url"/> is blank it falls back to Render's <c>RENDER_EXTERNAL_URL</c>, so it works on
/// Render with no configuration and stays idle locally (where neither is set).
/// </summary>
public sealed class KeepWarmOptions
{
    public const string SectionName = "KeepWarm";

    public bool Enabled { get; init; } = true;

    /// <summary>The service's public base URL. Blank → use <c>RENDER_EXTERNAL_URL</c>.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>How often to ping. Must be under Render's ~15-minute idle window; default leaves margin.</summary>
    public int IntervalSeconds { get; init; } = 300;
}
