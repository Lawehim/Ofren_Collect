using Microsoft.EntityFrameworkCore;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Subscriptions;

namespace OfrenCollect.Repository.Persistence.Repositories;

/// <inheritdoc />
public sealed class SubscriptionRepository : ISubscriptionRepository
{
    private readonly OfrenDbContext _db;

    public SubscriptionRepository(OfrenDbContext db) => _db = db;

    public void Add(Subscription subscription) => _db.Subscriptions.Add(subscription);

    public Task<Subscription?> GetByIdAsync(Guid subscriptionId, CancellationToken cancellationToken) =>
        _db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken);

    public Task<Subscription?> FindByReservedAccountNumberAsync(
        string accountNumber, CancellationToken cancellationToken) =>
        // Webhook path: no ambient tenant, so bypass the global filter. Reserved accounts are
        // globally unique, so this resolves at most one subscription (and thus its tenant).
        _db.Subscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.ReservedAccountNumber == accountNumber, cancellationToken);
}
