using Microsoft.EntityFrameworkCore;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Audit;

namespace OfrenCollect.Repository.Persistence.Repositories;

/// <inheritdoc />
public sealed class AuditReader : IAuditReader
{
    private readonly OfrenDbContext _db;

    public AuditReader(OfrenDbContext db) => _db = db;

    public async Task<IReadOnlyList<AuditEntryResponse>> GetForTenantAsync(
        Guid tenantId, int limit, CancellationToken cancellationToken) =>
        await _db.AuditEntries
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.TimestampUtc)
            .Take(limit)
            .Select(a => new AuditEntryResponse(
                a.Id, a.CorrelationId, a.Method, a.Path, a.ResponseStatusCode, a.DurationMs, a.IpAddress, a.TimestampUtc))
            .ToListAsync(cancellationToken);
}
