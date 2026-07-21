using OfrenCollect.Domain.Abstractions;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.Mandates;

/// <summary>
/// A direct-debit mandate authorising Ofren to auto-debit a customer for a subscription (FR-9).
/// Owned by the tenant that owns the subscription, so isolation is inherited (§8). The
/// <see cref="MandateReference"/> is unique and is the idempotency key for creation, so a retried
/// request is never a second mandate (§10). Bank details and consent are handled by Monnify's
/// authorisation flow and are deliberately not held here (§9 — minimise sensitive data); only the
/// authorisation lifecycle is tracked.
/// </summary>
public sealed class Mandate : AggregateRoot, ITenantOwned
{
    private Mandate()
    {
    }

    private Mandate(
        Guid id,
        Guid tenantId,
        Guid subscriptionId,
        string mandateReference,
        string monnifyMandateCode,
        DateTimeOffset requestedAt)
        : base(id)
    {
        TenantId = tenantId;
        SubscriptionId = subscriptionId;
        MandateReference = mandateReference;
        MonnifyMandateCode = monnifyMandateCode;
        Status = MandateStatus.Pending;
        RequestedAt = requestedAt;
    }

    public Guid TenantId { get; private set; }

    /// <summary>The subscription this mandate auto-debits.</summary>
    public Guid SubscriptionId { get; private set; }

    /// <summary>Our unique reference for this mandate — the idempotency key sent to Monnify.</summary>
    public string MandateReference { get; private set; } = string.Empty;

    /// <summary>Monnify's identifier for the mandate (returned when the mandate is created).</summary>
    public string MonnifyMandateCode { get; private set; } = string.Empty;

    public MandateStatus Status { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset? ActivatedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Whether the mandate may currently be debited.</summary>
    public bool IsActive => Status == MandateStatus.Active;

    /// <summary>
    /// Records a mandate created with Monnify (status INITIATED). <paramref name="monnifyMandateCode"/>
    /// is Monnify's identifier, returned at creation; the mandate is not debitable until the customer
    /// authorises it and it becomes <see cref="MandateStatus.Active"/>.
    /// </summary>
    public static Mandate Request(
        Guid tenantId, Guid subscriptionId, string mandateReference, string monnifyMandateCode,
        DateTimeOffset requestedAt)
    {
        Guard.AgainstNullOrWhiteSpace(mandateReference, nameof(mandateReference));
        Guard.AgainstNullOrWhiteSpace(monnifyMandateCode, nameof(monnifyMandateCode));

        return new Mandate(
            Guid.NewGuid(), tenantId, subscriptionId, mandateReference.Trim(), monnifyMandateCode.Trim(), requestedAt);
    }

    /// <summary>
    /// Records that the customer's authorisation was confirmed. Idempotent for a redelivered event;
    /// a revoked or expired mandate cannot be activated.
    /// </summary>
    public void Activate(DateTimeOffset activatedAt)
    {
        if (Status == MandateStatus.Active)
        {
            return;
        }

        if (Status != MandateStatus.Pending)
        {
            throw new InvalidOperationException($"A {Status} mandate cannot be activated.");
        }

        Status = MandateStatus.Active;
        ActivatedAt = activatedAt;
    }

    /// <summary>Revokes the mandate, stopping future debits. Idempotent; an expired mandate cannot be revoked.</summary>
    public void Revoke(DateTimeOffset revokedAt)
    {
        if (Status == MandateStatus.Revoked)
        {
            return;
        }

        if (Status == MandateStatus.Expired)
        {
            throw new InvalidOperationException("An expired mandate cannot be revoked.");
        }

        Status = MandateStatus.Revoked;
        RevokedAt = revokedAt;
    }
}
