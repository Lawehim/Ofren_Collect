namespace OfrenCollect.Application.Abstractions.Persistence;

/// <summary>Commits a unit of work as a single transaction.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
