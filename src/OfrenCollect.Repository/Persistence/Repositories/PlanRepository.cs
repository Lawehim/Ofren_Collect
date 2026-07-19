using Microsoft.EntityFrameworkCore;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Plans;

namespace OfrenCollect.Repository.Persistence.Repositories;

/// <inheritdoc />
public sealed class PlanRepository : IPlanRepository
{
    private readonly OfrenDbContext _db;

    public PlanRepository(OfrenDbContext db) => _db = db;

    public void Add(Plan plan) => _db.Plans.Add(plan);

    public async Task<IReadOnlyList<Plan>> ListAsync(CancellationToken cancellationToken) =>
        await _db.Plans.AsNoTracking().OrderBy(p => p.Name).ToListAsync(cancellationToken);

    public Task<Plan?> GetByIdAsync(Guid planId, CancellationToken cancellationToken) =>
        _db.Plans.FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);
}
