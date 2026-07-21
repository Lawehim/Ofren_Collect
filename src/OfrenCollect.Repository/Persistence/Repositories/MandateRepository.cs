using Microsoft.EntityFrameworkCore;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Mandates;

namespace OfrenCollect.Repository.Persistence.Repositories;

/// <inheritdoc />
public sealed class MandateRepository : IMandateRepository
{
    private readonly OfrenDbContext _db;

    public MandateRepository(OfrenDbContext db) => _db = db;

    public void Add(Mandate mandate) => _db.Mandates.Add(mandate);

    // Tenant-scoped by the global query filter (Mandate is tenant-owned); tracked so a status
    // change made on the authenticated refresh path is saved.
    public Task<Mandate?> GetByReferenceAsync(string mandateReference, CancellationToken cancellationToken) =>
        _db.Mandates.FirstOrDefaultAsync(m => m.MandateReference == mandateReference, cancellationToken);

    public Task<Mandate?> GetActiveBySubscriptionAsync(Guid subscriptionId, CancellationToken cancellationToken) =>
        _db.Mandates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.SubscriptionId == subscriptionId && m.Status == MandateStatus.Active, cancellationToken);

    // Activation/resolution path: bypass the global filter (no ambient tenant) and track so the
    // status change is saved. Mandate references are globally unique.
    public Task<Mandate?> GetForResolutionAsync(string mandateReference, CancellationToken cancellationToken) =>
        _db.Mandates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.MandateReference == mandateReference, cancellationToken);
}
