using OfrenCollect.Domain.Abstractions;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.Invoices;

/// <summary>
/// The amount due for one billing period of a subscription, and the money reconciled
/// against it. The reconciliation rules (FR-4.2–4.5) live here: applying a payment
/// accumulates what has been paid and derives the payment status.
/// </summary>
public sealed class Invoice : AggregateRoot, ITenantOwned
{
    /// <summary>Constructor for the persistence layer (EF Core).</summary>
    private Invoice()
    {
    }

    private Invoice(
        Guid id,
        Guid tenantId,
        Guid subscriptionId,
        Money amountDue,
        DateTimeOffset periodStart,
        DateTimeOffset dueDate)
        : base(id)
    {
        TenantId = tenantId;
        SubscriptionId = subscriptionId;
        AmountDue = amountDue;
        AmountPaid = Money.Zero(amountDue.Currency);
        PeriodStart = periodStart;
        DueDate = dueDate;
        Status = InvoiceStatus.Pending;
    }

    /// <summary>The owning tenant. Stamped on creation; never taken from client input.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>The subscription this invoice belongs to.</summary>
    public Guid SubscriptionId { get; private set; }

    /// <summary>The amount owed for the period.</summary>
    public Money AmountDue { get; private set; } = Money.Zero();

    /// <summary>The cumulative amount reconciled against this invoice.</summary>
    public Money AmountPaid { get; private set; } = Money.Zero();

    /// <summary>Start of the billing period this invoice covers (UTC).</summary>
    public DateTimeOffset PeriodStart { get; private set; }

    /// <summary>When the invoice falls due (UTC).</summary>
    public DateTimeOffset DueDate { get; private set; }

    /// <summary>The current payment status.</summary>
    public InvoiceStatus Status { get; private set; }

    /// <summary>What is still owed; zero once fully paid or overpaid.</summary>
    public Money OutstandingBalance =>
        AmountPaid < AmountDue ? AmountDue - AmountPaid : Money.Zero(AmountDue.Currency);

    /// <summary>Any surplus paid beyond the amount due; zero unless overpaid.</summary>
    public Money Credit =>
        AmountPaid > AmountDue ? AmountPaid - AmountDue : Money.Zero(AmountDue.Currency);

    /// <summary>Creates the first (pending) invoice for a subscription period.</summary>
    public static Invoice Create(
        Guid tenantId,
        Guid subscriptionId,
        Money amountDue,
        DateTimeOffset periodStart,
        DateTimeOffset dueDate) =>
        new(Guid.NewGuid(), tenantId, subscriptionId, amountDue, periodStart, dueDate);

    /// <summary>
    /// Applies an inflow to this invoice, accumulating the amount paid and re-deriving
    /// the status. The <see cref="Money"/> addition enforces currency safety.
    /// </summary>
    public void ApplyPayment(Money amount)
    {
        AmountPaid += amount;
        RecomputeStatus();
    }

    private void RecomputeStatus()
    {
        if (AmountPaid == Money.Zero(AmountDue.Currency))
        {
            Status = InvoiceStatus.Pending;
        }
        else if (AmountPaid < AmountDue)
        {
            Status = InvoiceStatus.Underpaid;
        }
        else if (AmountPaid == AmountDue)
        {
            Status = InvoiceStatus.Paid;
        }
        else
        {
            Status = InvoiceStatus.Overpaid;
        }
    }
}
