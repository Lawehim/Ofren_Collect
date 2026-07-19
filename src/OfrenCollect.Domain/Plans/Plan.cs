using OfrenCollect.Domain.Abstractions;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.Plans;

/// <summary>
/// A named recurring charge (amount + interval) a business bills its customers on.
/// Enforces FR-1.3: a non-blank name and an amount greater than zero.
/// </summary>
public sealed class Plan : AggregateRoot, ITenantOwned
{
    private Plan()
    {
    }

    private Plan(Guid id, Guid tenantId, string name, Money amount, BillingInterval interval)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Amount = amount;
        Interval = interval;
        IsActive = true;
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public Money Amount { get; private set; } = Money.Zero();

    public BillingInterval Interval { get; private set; }

    /// <summary>Whether the plan can be used for new subscriptions (FR-1.4).</summary>
    public bool IsActive { get; private set; }

    public static Plan Create(Guid tenantId, string name, Money amount, BillingInterval interval)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        Guard.AgainstNonPositive(amount, nameof(amount));

        return new Plan(Guid.NewGuid(), tenantId, name.Trim(), amount, interval);
    }

    /// <summary>Retires the plan so no new subscriptions can be created against it.</summary>
    public void Deactivate() => IsActive = false;
}
