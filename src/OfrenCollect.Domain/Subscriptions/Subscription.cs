using OfrenCollect.Domain.Abstractions;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.Subscriptions;

/// <summary>
/// A customer enrolled on a plan, with a dedicated Monnify reserved account. The reserved
/// account it is paid into is what makes reconciliation zero-touch: it identifies the
/// subscription (and therefore the tenant) uniquely.
/// </summary>
public sealed class Subscription : AggregateRoot, ITenantOwned
{
    private Subscription()
    {
    }

    private Subscription(
        Guid id,
        Guid tenantId,
        Guid customerId,
        Guid planId,
        string reservedAccountReference,
        DateTimeOffset nextDueDate)
        : base(id)
    {
        TenantId = tenantId;
        CustomerId = customerId;
        PlanId = planId;
        ReservedAccountReference = reservedAccountReference;
        NextDueDate = nextDueDate;
        Status = SubscriptionStatus.Active;
    }

    public Guid TenantId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid PlanId { get; private set; }

    /// <summary>Our unique reference sent to Monnify when provisioning the reserved account.</summary>
    public string ReservedAccountReference { get; private set; } = string.Empty;

    /// <summary>The reserved account number returned by Monnify; null until provisioned.</summary>
    public string? ReservedAccountNumber { get; private set; }

    /// <summary>The reserved account's bank name from Monnify; null until provisioned.</summary>
    public string? ReservedBankName { get; private set; }

    public DateTimeOffset NextDueDate { get; private set; }

    public SubscriptionStatus Status { get; private set; }

    /// <summary>Enrols a customer on a plan, creating an active subscription (FR-2.2).</summary>
    public static Subscription Enrol(
        Guid tenantId,
        Guid customerId,
        Guid planId,
        string reservedAccountReference,
        DateTimeOffset nextDueDate)
    {
        Guard.AgainstNullOrWhiteSpace(reservedAccountReference, nameof(reservedAccountReference));

        return new Subscription(
            Guid.NewGuid(), tenantId, customerId, planId, reservedAccountReference.Trim(), nextDueDate);
    }

    /// <summary>Records the reserved account details returned by Monnify (FR-2.3).</summary>
    public void AttachReservedAccount(string accountNumber, string bankName)
    {
        Guard.AgainstNullOrWhiteSpace(accountNumber, nameof(accountNumber));
        Guard.AgainstNullOrWhiteSpace(bankName, nameof(bankName));

        ReservedAccountNumber = accountNumber.Trim();
        ReservedBankName = bankName.Trim();
    }

    /// <summary>Cancels the subscription so it is no longer invoiced (FR-2.7).</summary>
    public void Cancel() => Status = SubscriptionStatus.Cancelled;

    /// <summary>Flags an active subscription as overdue (FR-4.8). Cancelled subscriptions are unaffected.</summary>
    public void MarkOverdue()
    {
        if (Status == SubscriptionStatus.Active)
        {
            Status = SubscriptionStatus.Overdue;
        }
    }
}
