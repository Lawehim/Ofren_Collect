namespace OfrenCollect.Domain.Abstractions;

/// <summary>
/// Marks an entity as belonging to exactly one tenant. The persistence layer uses this to
/// apply the global tenant query filter on reads and to stamp the tenant on writes, so
/// isolation is the default rather than something each query must remember (NFR-1.7).
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}
