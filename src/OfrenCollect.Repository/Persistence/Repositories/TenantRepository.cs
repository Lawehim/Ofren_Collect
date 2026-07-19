using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Tenants;

namespace OfrenCollect.Repository.Persistence.Repositories;

/// <inheritdoc />
public sealed class TenantRepository : ITenantRepository
{
    private readonly OfrenDbContext _db;

    public TenantRepository(OfrenDbContext db) => _db = db;

    public void Add(Tenant tenant) => _db.Tenants.Add(tenant);
}
