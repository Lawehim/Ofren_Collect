namespace OfrenCollect.Application.Audit;

/// <summary>An audit entry as returned to an Owner querying their tenant's trail.</summary>
public sealed record AuditEntryResponse(
    Guid Id,
    string CorrelationId,
    string Method,
    string Path,
    int ResponseStatusCode,
    long DurationMs,
    string? IpAddress,
    DateTimeOffset TimestampUtc);
