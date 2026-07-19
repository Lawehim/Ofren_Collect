namespace OfrenCollect.SharedKernel;

/// <summary>
/// Base class for domain entities. Identity is by <see cref="Id"/> and concrete type:
/// two entities are equal only if they are the same type and share a non-empty id.
/// Transient entities (an empty <see cref="Id"/>) are never equal to one another.
/// </summary>
public abstract class Entity : IEquatable<Entity>
{
    /// <summary>Parameterless constructor for the persistence layer (EF Core).</summary>
    protected Entity()
    {
    }

    protected Entity(Guid id)
    {
        Id = id;
    }

    /// <summary>The entity's identity.</summary>
    public Guid Id { get; protected init; }

    public bool Equals(Entity? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        if (Id == Guid.Empty || other.Id == Guid.Empty)
        {
            return false;
        }

        return Id == other.Id;
    }

    public override bool Equals(object? obj) => obj is Entity entity && Equals(entity);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Entity? left, Entity? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity? left, Entity? right) => !(left == right);
}
