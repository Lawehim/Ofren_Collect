namespace OfrenCollect.SharedKernel;

/// <summary>
/// Base class for aggregate roots — the entities a repository loads and saves as a unit,
/// and the consistency boundary for domain invariants.
/// </summary>
public abstract class AggregateRoot : Entity
{
    protected AggregateRoot()
    {
    }

    protected AggregateRoot(Guid id) : base(id)
    {
    }
}
