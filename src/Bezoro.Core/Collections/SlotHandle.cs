namespace Bezoro.Core.Collections;

/// <summary>Identifies one occupied slot in the map that created it.</summary>
/// <remarks>Default, removed, cleared, and foreign-map handles never identify a live value.</remarks>
public readonly struct SlotHandle : IEquatable<SlotHandle>
{
	internal SlotHandle(long owner, int index, uint generation)
	{
		Owner = owner;
		Index = index;
		Generation = generation;
	}

	internal long Owner { get; }
	internal int Index { get; }
	internal uint Generation { get; }

	/// <summary>Reports whether this is the default, unassigned handle.</summary>
	/// <remarks>A non-default handle can still be stale. Query its map to test liveness.</remarks>
	public bool IsDefault => Owner == 0;

	/// <inheritdoc />
	public bool Equals(SlotHandle other) =>
		Owner == other.Owner && Index == other.Index && Generation == other.Generation;

	/// <inheritdoc />
	public override bool Equals(object? obj) => obj is SlotHandle other && Equals(other);

	/// <inheritdoc />
	public override int GetHashCode() => HashCode.Combine(Owner, Index, Generation);

	/// <summary>Compares handles, including their owning map and generation.</summary>
	public static bool operator ==(SlotHandle left, SlotHandle right) => left.Equals(right);

	/// <summary>Compares handles, including their owning map and generation.</summary>
	public static bool operator !=(SlotHandle left, SlotHandle right) => !left.Equals(right);
}
