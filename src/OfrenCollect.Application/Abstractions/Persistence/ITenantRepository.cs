using OfrenCollect.Domain.Tenants;

namespace OfrenCollect.Application.Abstractions.Persistence;

/// <summary>Writes tenants (created on self-service registration).</summary>
public interface ITenantRepository
{
    void Add(Tenant tenant);
}
