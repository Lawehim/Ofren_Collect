using OfrenCollect.Domain.Abstractions;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.Refunds;

/// <summary>
/// A refund of money to a customer against an original, already-verified transaction (FR-11).
/// Owned by the tenant that owns the original transaction, so isolation is inherited rather than
/// taken from client input (§8). The <see cref="RefundReference"/> is unique and is the
/// idempotency key, so a retried or redelivered request is never a double refund (§10).
/// </summary>
public sealed class Refund : AggregateRoot, ITenantOwned
{
    private Refund()
    {
    }

    private Refund(
        Guid id,
        Guid tenantId,
        string originalTransactionReference,
        string refundReference,
        Money amount,
        string reason,
        DateTimeOffset requestedAt)
        : base(id)
    {
        TenantId = tenantId;
        OriginalTransactionReference = originalTransactionReference;
        RefundReference = refundReference;
        Amount = amount;
        Reason = reason;
        Status = RefundStatus.Requested;
        RequestedAt = requestedAt;
    }

    public Guid TenantId { get; private set; }

    /// <summary>The Monnify reference of the original payment being refunded.</summary>
    public string OriginalTransactionReference { get; private set; } = string.Empty;

    /// <summary>Our unique reference for this refund — the idempotency key sent to Monnify.</summary>
    public string RefundReference { get; private set; } = string.Empty;

    public Money Amount { get; private set; } = Money.Zero();

    public string Reason { get; private set; } = string.Empty;

    public RefundStatus Status { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    /// <summary>When the refund reached a terminal state; null while still <see cref="RefundStatus.Requested"/>.</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>
    /// Initiates a refund. <paramref name="maximumRefundable"/> is the original transaction amount
    /// less any refunds already made against it; the requested amount must be positive and must not
    /// exceed it (FR-11.1), so cumulative refunds can never over-refund a transaction.
    /// </summary>
    public static Refund Request(
        Guid tenantId,
        string originalTransactionReference,
        string refundReference,
        Money amount,
        string reason,
        Money maximumRefundable,
        DateTimeOffset requestedAt)
    {
        Guard.AgainstNullOrWhiteSpace(originalTransactionReference, nameof(originalTransactionReference));
        Guard.AgainstNullOrWhiteSpace(refundReference, nameof(refundReference));
        Guard.AgainstNullOrWhiteSpace(reason, nameof(reason));
        Guard.AgainstNonPositive(amount, nameof(amount));
        ArgumentNullException.ThrowIfNull(maximumRefundable);

        if (amount > maximumRefundable)
        {
            throw new ArgumentException(
                "Refund amount cannot exceed the transaction's remaining refundable amount.", nameof(amount));
        }

        return new Refund(
            Guid.NewGuid(), tenantId, originalTransactionReference.Trim(), refundReference.Trim(),
            amount, reason.Trim(), requestedAt);
    }

    /// <summary>
    /// Records that Monnify completed the refund. Idempotent — a redelivered success webhook is a
    /// no-op — but a refund already marked failed cannot be completed.
    /// </summary>
    public void MarkCompleted(DateTimeOffset completedAt) => Resolve(RefundStatus.Completed, completedAt);

    /// <summary>
    /// Records that the refund failed. Idempotent for a redelivered failure webhook; a refund
    /// already completed cannot be marked failed.
    /// </summary>
    public void MarkFailed(DateTimeOffset failedAt) => Resolve(RefundStatus.Failed, failedAt);

    private void Resolve(RefundStatus terminal, DateTimeOffset resolvedAt)
    {
        if (Status == terminal)
        {
            return;
        }

        if (Status != RefundStatus.Requested)
        {
            throw new InvalidOperationException(
                $"A {Status} refund cannot transition to {terminal}.");
        }

        Status = terminal;
        ResolvedAt = resolvedAt;
    }
}
