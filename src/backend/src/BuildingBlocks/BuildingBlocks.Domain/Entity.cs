namespace Laundry.BuildingBlocks.Domain;

public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    protected Entity(TId id)
    {
        Id = id;
    }

    public TId Id { get; protected init; }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null || GetType() != other.GetType())
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return !IsDefaultId() && EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    public override int GetHashCode() =>
        IsDefaultId()
            ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this)
            : HashCode.Combine(GetType(), Id);

    private bool IsDefaultId() => EqualityComparer<TId>.Default.Equals(Id, default!);
}
