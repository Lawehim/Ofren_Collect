using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.Tenants;

/// <summary>
/// A business using the system — the isolation boundary for all tenant-owned data.
/// Created by self-service registration (FR-0.1).
/// </summary>
public sealed class Tenant : AggregateRoot
{
    private Tenant()
    {
    }

    private Tenant(Guid id, string businessName, DateTimeOffset createdAt)
        : base(id)
    {
        BusinessName = businessName;
        CreatedAt = createdAt;
    }

    public string BusinessName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static Tenant Register(string businessName, DateTimeOffset createdAt)
    {
        Guard.AgainstNullOrWhiteSpace(businessName, nameof(businessName));

        return new Tenant(Guid.NewGuid(), businessName.Trim(), createdAt);
    }
}
