using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.Customers;

/// <summary>An individual or business a tenant bills. Requires a non-blank name and email.</summary>
public sealed class Customer : AggregateRoot
{
    private Customer()
    {
    }

    private Customer(Guid id, Guid tenantId, string name, string email)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Email = email;
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public static Customer Register(Guid tenantId, string name, string email)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        Guard.AgainstNullOrWhiteSpace(email, nameof(email));

        return new Customer(Guid.NewGuid(), tenantId, name.Trim(), email.Trim().ToLowerInvariant());
    }
}
