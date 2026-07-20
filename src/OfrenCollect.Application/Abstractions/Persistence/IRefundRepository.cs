using OfrenCollect.Domain.Refunds;

namespace OfrenCollect.Application.Abstractions.Persistence;

/// <summary>Stores refunds and answers the idempotency and remaining-refundable questions.</summary>
public interface IRefundRepository
{
    void Add(Refund refund);

    /// <summary>The refund with this reference, or null — the idempotency lookup (FR-11.3).</summary>
    Task<Refund?> GetByReferenceAsync(string refundReference, CancellationToken cancellationToken);

    /// <summary>
    /// The refund with this reference for the webhook-driven resolution path (FR-11.4): tracked (so
    /// its status change is saved) and tenant-filter-bypassing, because the refund webhook carries
    /// no ambient tenant. The refund reference is globally unique, so this resolves at most one.
    /// </summary>
    Task<Refund?> GetForResolutionAsync(string refundReference, CancellationToken cancellationToken);

    /// <summary>
    /// Total of all non-failed refunds already made against an original transaction, so a new
    /// refund cannot push the cumulative amount past the transaction's value (FR-11.1).
    /// </summary>
    Task<decimal> TotalRefundedForTransactionAsync(
        string originalTransactionReference, CancellationToken cancellationToken);
}
