using OfrenCollect.Domain.Subscriptions;

namespace OfrenCollect.Application.Abstractions.Persistence;

/// <summary>Reads subscriptions for reconciliation.</summary>
public interface ISubscriptionRepository
{
    void Add(Subscription subscription);

    /// <summary>
    /// Finds the subscription that owns a reserved account number. Used on the webhook path,
    /// which has no ambient tenant, so the implementation deliberately bypasses the global
    /// tenant filter — reserved accounts are globally unique, so this resolves exactly one
    /// subscription and therefore its tenant (§11.3). Returns null if none matches.
    /// </summary>
    Task<Subscription?> FindByReservedAccountNumberAsync(string accountNumber, CancellationToken cancellationToken);
}
