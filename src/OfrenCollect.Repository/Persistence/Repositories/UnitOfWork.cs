using OfrenCollect.Application.Abstractions.Persistence;

namespace OfrenCollect.Repository.Persistence.Repositories;

/// <inheritdoc />
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly OfrenDbContext _db;

    public UnitOfWork(OfrenDbContext db) => _db = db;

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);
}
