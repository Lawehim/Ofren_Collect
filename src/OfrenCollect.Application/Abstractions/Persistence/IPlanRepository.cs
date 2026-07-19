using OfrenCollect.Domain.Plans;

namespace OfrenCollect.Application.Abstractions.Persistence;

/// <summary>Reads and writes plans (tenant-scoped by the global query filter).</summary>
public interface IPlanRepository
{
    void Add(Plan plan);

    /// <summary>The current tenant's plans, newest-relevant first.</summary>
    Task<IReadOnlyList<Plan>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Finds a plan owned by the current tenant, or null.</summary>
    Task<Plan?> GetByIdAsync(Guid planId, CancellationToken cancellationToken);
}
