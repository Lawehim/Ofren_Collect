using OfrenCollect.Domain.Mandates;

namespace OfrenCollect.Application.Abstractions.Persistence;

/// <summary>Stores direct-debit mandates (FR-9).</summary>
public interface IMandateRepository
{
    void Add(Mandate mandate);

    /// <summary>A mandate by reference for the current tenant, or null.</summary>
    Task<Mandate?> GetByReferenceAsync(string mandateReference, CancellationToken cancellationToken);

    /// <summary>The current tenant's active mandate for a subscription, or null (used to debit).</summary>
    Task<Mandate?> GetActiveBySubscriptionAsync(Guid subscriptionId, CancellationToken cancellationToken);

    /// <summary>
    /// A mandate by reference for the activation/resolution path: tracked (so its status change is
    /// saved) and tenant-filter-bypassing, for when there is no ambient tenant. References are
    /// globally unique.
    /// </summary>
    Task<Mandate?> GetForResolutionAsync(string mandateReference, CancellationToken cancellationToken);
}
