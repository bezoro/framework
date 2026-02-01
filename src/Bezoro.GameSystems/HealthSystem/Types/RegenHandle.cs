using System;

namespace Bezoro.GameSystems.HealthSystem.Types;

/// <summary>
///     A lightweight handle to a health regeneration instance. Value 0 represents an invalid/uninitialized handle.
/// </summary>
public readonly struct RegenHandle : IEquatable<RegenHandle>
{
	/// <summary>
	///     A handle representing no regen (ID = 0).
	/// </summary>
	public static readonly RegenHandle None = default;

	/// <summary>
	///     The unique identifier for this regen.
	/// </summary>
	public readonly int Id;

	/// <summary>
	///     Creates a new regen handle with the specified ID.
	/// </summary>
	/// <param name="id">The unique regen identifier.</param>
	public RegenHandle(int id)
	{
		Id = id;
	}

	/// <summary>
	///     Gets whether this handle refers to a valid regen (ID &gt; 0).
	/// </summary>
	public bool IsValid => Id > 0;

	public static bool operator ==(RegenHandle left, RegenHandle right) => left.Id == right.Id;
	public static bool operator !=(RegenHandle left, RegenHandle right) => left.Id != right.Id;

	#region Equality

	/// <inheritdoc />
	public bool Equals(RegenHandle other) => Id == other.Id;

	/// <inheritdoc />
	public override bool Equals(object? obj) => obj is RegenHandle other && Equals(other);

	/// <inheritdoc />
	public override int GetHashCode() => Id;

	#endregion

	/// <inheritdoc />
	public override string ToString() => $"Regen({Id})";
}
