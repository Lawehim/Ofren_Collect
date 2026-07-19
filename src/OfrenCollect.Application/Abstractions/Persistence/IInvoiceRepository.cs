using OfrenCollect.Domain.Invoices;

namespace OfrenCollect.Application.Abstractions.Persistence;

/// <summary>Reads invoices for reconciliation.</summary>
public interface IInvoiceRepository
{
    /// <summary>
    /// Returns the subscription's current open invoice (Pending or Underpaid), or null if
    /// none is open. Resolved on the webhook path, so the implementation bypasses the global
    /// tenant filter and scopes explicitly by subscription.
    /// </summary>
    Task<Invoice?> GetOpenInvoiceForSubscriptionAsync(Guid subscriptionId, CancellationToken cancellationToken);
}
