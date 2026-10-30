namespace Bezoro.Simulation.Attributes;

/// <summary>An opaque owner-scoped handle to a modifier. Handles are not persistent save identifiers.</summary>
public readonly struct StatModifierHandle : IEquatable<StatModifierHandle>
{
    internal StatModifierHandle(long owner, int slot, uint generation)
    {
        Owner = owner;
        Slot = slot;
        Generation = generation;
    }

    internal long Owner { get; }
    internal int Slot { get; }
    internal uint Generation { get; }

    /// <summary>Gets whether this handle is the uninitialized default value.</summary>
    public bool IsDefault => Owner == 0;

    /// <inheritdoc />
    public bool Equals(StatModifierHandle other) =>
        Owner == other.Owner && Slot == other.Slot && Generation == other.Generation;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is StatModifierHandle other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => unchecked((Owner.GetHashCode() * 397 ^ Slot) * 397 ^ (int)Generation);

    /// <summary>Compares handles.</summary>
    public static bool operator ==(StatModifierHandle left, StatModifierHandle right) => left.Equals(right);

    /// <summary>Compares handles.</summary>
    public static bool operator !=(StatModifierHandle left, StatModifierHandle right) => !left.Equals(right);
}
