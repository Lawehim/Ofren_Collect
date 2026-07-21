using OfrenCollect.Domain.Abstractions;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.Mandates;

/// <summary>
/// A single debit against a mandate for an invoice (FR-9.3). Tracks the debit from initiation to
/// its terminal state. The <see cref="PaymentReference"/> is unique and is the idempotency key, and
/// the <see cref="MandateDebitStatus"/> makes reconciliation safe to run twice (§10).
/// </summary>
public sealed class MandateDebit : AggregateRoot, ITenantOwned
{
    private MandateDebit()
    {
    }

    private MandateDebit(
        Guid id,
        Guid tenantId,
        string mandateReference,
        Guid invoiceId,
        string paymentReference,
        string transactionReference,
        Money amount,
        DateTimeOffset initiatedAt)
        : base(id)
    {
        TenantId = tenantId;
        MandateReference = mandateReference;
        InvoiceId = invoiceId;
        PaymentReference = paymentReference;
        TransactionReference = transactionReference;
        Amount = amount;
        Status = MandateDebitStatus.Pending;
        InitiatedAt = initiatedAt;
    }

    public Guid TenantId { get; private set; }

    public string MandateReference { get; private set; } = string.Empty;

    /// <summary>The invoice this debit pays — how the debit reconciles without a reserved account.</summary>
    public Guid InvoiceId { get; private set; }

    /// <summary>Our unique reference for this debit — the idempotency key sent to Monnify.</summary>
    public string PaymentReference { get; private set; } = string.Empty;

    /// <summary>Monnify's transaction reference for the debit.</summary>
    public string TransactionReference { get; private set; } = string.Empty;

    public Money Amount { get; private set; } = Money.Zero();

    public MandateDebitStatus Status { get; private set; }

    public DateTimeOffset InitiatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public static MandateDebit Initiate(
        Guid tenantId,
        string mandateReference,
        Guid invoiceId,
        string paymentReference,
        string transactionReference,
        Money amount,
        DateTimeOffset initiatedAt)
    {
        Guard.AgainstNullOrWhiteSpace(mandateReference, nameof(mandateReference));
        Guard.AgainstNullOrWhiteSpace(paymentReference, nameof(paymentReference));
        Guard.AgainstNullOrWhiteSpace(transactionReference, nameof(transactionReference));
        Guard.AgainstNonPositive(amount, nameof(amount));

        return new MandateDebit(
            Guid.NewGuid(), tenantId, mandateReference.Trim(), invoiceId, paymentReference.Trim(),
            transactionReference.Trim(), amount, initiatedAt);
    }

    /// <summary>Records that the debit succeeded. Idempotent; a failed debit cannot be marked paid.</summary>
    public void MarkPaid(DateTimeOffset paidAt) => Resolve(MandateDebitStatus.Paid, paidAt);

    /// <summary>Records that the debit failed. Idempotent; a paid debit cannot be marked failed.</summary>
    public void MarkFailed(DateTimeOffset failedAt) => Resolve(MandateDebitStatus.Failed, failedAt);

    private void Resolve(MandateDebitStatus terminal, DateTimeOffset resolvedAt)
    {
        if (Status == terminal)
        {
            return;
        }

        if (Status != MandateDebitStatus.Pending)
        {
            throw new InvalidOperationException($"A {Status} debit cannot transition to {terminal}.");
        }

        Status = terminal;
        CompletedAt = resolvedAt;
    }
}
