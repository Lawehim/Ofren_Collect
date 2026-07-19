namespace OfrenCollect.Application.Abstractions;

/// <summary>
/// The tenant the current unit of work acts for. On authenticated requests this is derived
/// from the JWT (never from client input, CLAUDE.md §8). On the webhook path there is no
/// ambient tenant — the tenant is resolved from the reserved account instead — so
/// <see cref="CurrentTenantId"/> is null and tenant-owned writes carry the tenant set on the
/// entity by the reconciliation logic.
/// </summary>
public interface ITenantContext
{
    /// <summary>The acting tenant, or null when there is no ambient tenant (e.g. the webhook path).</summary>
    Guid? CurrentTenantId { get; }

    /// <summary>Whether an ambient tenant is present.</summary>
    bool HasTenant => CurrentTenantId.HasValue;
}
