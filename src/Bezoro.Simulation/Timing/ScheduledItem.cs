namespace Bezoro.Simulation.Timing;

/// <summary>A scheduled payload and its original due tick. Payload references are not deep-copied.</summary>
public readonly record struct ScheduledItem<T>
{
    internal ScheduledItem(ScheduledHandle handle, long dueTick, T value)
    {
        Handle = handle;
        DueTick = dueTick;
        Value = value;
    }

    /// <summary>Gets the item's identity. It becomes stale when drained or canceled.</summary>
    public ScheduledHandle Handle { get; }

    /// <summary>Gets the absolute simulation tick at which this item became due.</summary>
    public long DueTick { get; }

    /// <summary>Gets the caller-owned payload.</summary>
    public T Value { get; }
}
