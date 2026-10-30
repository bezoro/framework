using System.Threading;

namespace Bezoro.Simulation.Timing;

/// <summary>An opaque, owner-scoped identity for one scheduled item. Handles are not persistent save identifiers.</summary>
public readonly struct ScheduledHandle : IEquatable<ScheduledHandle>
{
    internal ScheduledHandle(long ownerId, int slot, uint generation)
    {
        OwnerId = ownerId;
        Slot = slot;
        Generation = generation;
    }

    internal long OwnerId { get; }
    internal int Slot { get; }
    internal uint Generation { get; }

    /// <summary>Gets whether this is the empty handle. Nonempty handles may still be stale.</summary>
    public bool IsDefault => OwnerId == 0;

    /// <summary>Compares the owner, slot, and generation.</summary>
    public bool Equals(ScheduledHandle other) =>
        OwnerId == other.OwnerId && Slot == other.Slot && Generation == other.Generation;

    /// <summary>Compares this handle with a boxed handle.</summary>
    public override bool Equals(object? obj) => obj is ScheduledHandle other && Equals(other);

    /// <summary>Returns an in-process hash for this identity.</summary>
    public override int GetHashCode() => HashCode.Combine(OwnerId, Slot, Generation);

    /// <summary>Compares handles for equality.</summary>
    public static bool operator ==(ScheduledHandle left, ScheduledHandle right) => left.Equals(right);

    /// <summary>Compares handles for inequality.</summary>
    public static bool operator !=(ScheduledHandle left, ScheduledHandle right) => !left.Equals(right);
}

internal static class SchedulerOwnerIds
{
    private static long _lastId;

    internal static long Next()
    {
        while (true)
        {
            long previous = Volatile.Read(ref _lastId);
            if (previous == long.MaxValue)
                throw new InvalidOperationException("Scheduler owner identities have been exhausted.");
            long next = previous + 1;
            if (Interlocked.CompareExchange(ref _lastId, next, previous) == previous)
                return next;
        }
    }
}
