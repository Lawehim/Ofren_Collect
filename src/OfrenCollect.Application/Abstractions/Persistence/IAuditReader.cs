using OfrenCollect.Application.Audit;

namespace OfrenCollect.Application.Abstractions.Persistence;

/// <summary>Reads a tenant's audit trail. Scoped explicitly by tenant (FR-8.3).</summary>
public interface IAuditReader
{
    Task<IReadOnlyList<AuditEntryResponse>> GetForTenantAsync(
        Guid tenantId, int limit, CancellationToken cancellationToken);
}
