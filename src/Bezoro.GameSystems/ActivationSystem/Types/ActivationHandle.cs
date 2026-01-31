using System;

namespace Bezoro.GameSystems.ActivationSystem.Types;

/// <summary>
///     A lightweight handle to an activation entry. Value 0 represents an invalid/uninitialized handle.
/// </summary>
public readonly struct ActivationHandle : IEquatable<ActivationHandle>
{
	/// <summary>
	///     A handle representing no activation entry (ID = 0).
	/// </summary>
	public static readonly ActivationHandle None = default;

	/// <summary>
	///     The unique identifier for this activation entry.
	/// </summary>
	public readonly int Id;

	/// <summary>
	///     Creates a new activation handle with the specified ID.
	/// </summary>
	/// <param name="id">The unique activation identifier.</param>
	public ActivationHandle(int id)
	{
		Id = id;
	}

	/// <summary>
	///     Gets whether this handle refers to a valid activation entry (ID &gt; 0).
	/// </summary>
	public bool IsValid => Id > 0;

	public static bool operator ==(ActivationHandle left, ActivationHandle right) => left.Id == right.Id;
	public static bool operator !=(ActivationHandle left, ActivationHandle right) => left.Id != right.Id;

	#region Equality

	/// <inheritdoc />
	public bool Equals(ActivationHandle other) => Id == other.Id;

	/// <inheritdoc />
	public override bool Equals(object? obj) => obj is ActivationHandle other && Equals(other);

	/// <inheritdoc />
	public override int GetHashCode() => Id;

	#endregion

	/// <inheritdoc />
	public override string ToString() => $"Activation({Id})";
}
