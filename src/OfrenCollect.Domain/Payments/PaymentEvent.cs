using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.Payments;

/// <summary>
/// A recorded inflow into a reserved account. The Monnify transaction reference is unique
/// and is the idempotency key (FR-3.6, NFR-2.1). An inflow that matches no subscription is
/// recorded tenant-less and unmatched (FR-4.7) rather than discarded.
/// </summary>
public sealed class PaymentEvent : AggregateRoot
{
    private PaymentEvent()
    {
    }

    private PaymentEvent(
        Guid id,
        Guid? tenantId,
        string monnifyTransactionReference,
        string reservedAccountNumber,
        Money amount,
        DateTimeOffset paidAt,
        Guid? matchedInvoiceId)
        : base(id)
    {
        TenantId = tenantId;
        MonnifyTransactionReference = monnifyTransactionReference;
        ReservedAccountNumber = reservedAccountNumber;
        Amount = amount;
        PaidAt = paidAt;
        MatchedInvoiceId = matchedInvoiceId;
    }

    /// <summary>The owning tenant; null while the inflow is unmatched.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>The unique Monnify transaction reference — the idempotency key.</summary>
    public string MonnifyTransactionReference { get; private set; } = string.Empty;

    /// <summary>The reserved account the money landed in.</summary>
    public string ReservedAccountNumber { get; private set; } = string.Empty;

    public Money Amount { get; private set; } = Money.Zero();

    public DateTimeOffset PaidAt { get; private set; }

    /// <summary>The invoice this inflow was applied to; null while unmatched.</summary>
    public Guid? MatchedInvoiceId { get; private set; }

    /// <summary>Whether the inflow has been resolved to an invoice.</summary>
    public bool IsMatched => MatchedInvoiceId.HasValue;

    /// <summary>Records an inflow resolved to an owning tenant and invoice.</summary>
    public static PaymentEvent RecordMatched(
        Guid tenantId,
        string monnifyTransactionReference,
        string reservedAccountNumber,
        Money amount,
        DateTimeOffset paidAt,
        Guid matchedInvoiceId)
    {
        Guard.AgainstNullOrWhiteSpace(monnifyTransactionReference, nameof(monnifyTransactionReference));
        Guard.AgainstNullOrWhiteSpace(reservedAccountNumber, nameof(reservedAccountNumber));

        return new PaymentEvent(
            Guid.NewGuid(), tenantId, monnifyTransactionReference.Trim(), reservedAccountNumber.Trim(),
            amount, paidAt, matchedInvoiceId);
    }

    /// <summary>Records an inflow that matched no subscription (FR-4.7).</summary>
    public static PaymentEvent RecordUnmatched(
        string monnifyTransactionReference,
        string reservedAccountNumber,
        Money amount,
        DateTimeOffset paidAt)
    {
        Guard.AgainstNullOrWhiteSpace(monnifyTransactionReference, nameof(monnifyTransactionReference));
        Guard.AgainstNullOrWhiteSpace(reservedAccountNumber, nameof(reservedAccountNumber));

        return new PaymentEvent(
            Guid.NewGuid(), tenantId: null, monnifyTransactionReference.Trim(), reservedAccountNumber.Trim(),
            amount, paidAt, matchedInvoiceId: null);
    }
}
