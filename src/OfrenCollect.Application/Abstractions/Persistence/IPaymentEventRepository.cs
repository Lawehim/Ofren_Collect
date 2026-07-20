using OfrenCollect.Domain.Payments;

namespace OfrenCollect.Application.Abstractions.Persistence;

/// <summary>Stores payment events and answers the idempotency question.</summary>
public interface IPaymentEventRepository
{
    /// <summary>Whether a payment with this Monnify reference has already been recorded (FR-3.6).</summary>
    Task<bool> ExistsByReferenceAsync(string transactionReference, CancellationToken cancellationToken);

    /// <summary>
    /// The matched (tenant-owned) payment with this reference, or null. Scoped to the current tenant
    /// (PaymentEvent sits outside the global filter, so the implementation scopes explicitly), so it
    /// never returns another tenant's or an unmatched (tenant-less) payment — which is what stops a
    /// refund of a payment the caller doesn't own (FR-11.1).
    /// </summary>
    Task<PaymentEvent?> GetMatchedByReferenceAsync(string transactionReference, CancellationToken cancellationToken);

    void Add(PaymentEvent paymentEvent);
}
