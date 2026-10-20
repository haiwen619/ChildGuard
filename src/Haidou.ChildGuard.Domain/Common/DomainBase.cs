using System;
using System.Collections.Generic;

namespace Haidou.ChildGuard.Domain.Common;

public interface IAggregateRoot { }

public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}

public abstract class Entity<TKey>
{
    public TKey Id { get; protected set; } = default!;
}

public abstract class ValueObject
{
    protected abstract IEnumerable<object> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj == null || obj.GetType() != GetType()) return false;
        var other = (ValueObject)obj;
        using var thisValues = GetEqualityComponents().GetEnumerator();
        using var otherValues = other.GetEqualityComponents().GetEnumerator();
        while (thisValues.MoveNext() && otherValues.MoveNext())
        {
            if (!Equals(thisValues.Current, otherValues.Current)) return false;
        }
        return !thisValues.MoveNext() && !otherValues.MoveNext();
    }

    public override int GetHashCode()
    {
        int hash = 17;
        foreach (var obj in GetEqualityComponents())
        {
            hash = hash * 31 + (obj?.GetHashCode() ?? 0);
        }
        return hash;
    }
}
