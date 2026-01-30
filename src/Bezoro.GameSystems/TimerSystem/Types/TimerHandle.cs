using System;

namespace Bezoro.GameSystems.TimerSystem.Types;

/// <summary>
///     A lightweight handle to a timer instance. Value 0 represents an invalid/uninitialized handle.
/// </summary>
public readonly struct TimerHandle : IEquatable<TimerHandle>
{
	/// <summary>
	///     A handle representing no timer (ID = 0).
	/// </summary>
	public static readonly TimerHandle None = default;

	/// <summary>
	///     The unique identifier for this timer.
	/// </summary>
	public readonly int Id;

	/// <summary>
	///     Creates a new timer handle with the specified ID.
	/// </summary>
	/// <param name="id">The unique timer identifier.</param>
	public TimerHandle(int id) => Id = id;

	/// <summary>
	///     Gets whether this handle refers to a valid timer (ID &gt; 0).
	/// </summary>
	public bool IsValid => Id > 0;

	public static bool operator ==(TimerHandle left, TimerHandle right) => left.Id == right.Id;
	public static bool operator !=(TimerHandle left, TimerHandle right) => left.Id != right.Id;

	/// <inheritdoc />
	public bool Equals(TimerHandle other) => Id == other.Id;

	/// <inheritdoc />
	public override bool Equals(object? obj) => obj is TimerHandle other && Equals(other);

	/// <inheritdoc />
	public override int GetHashCode() => Id;

	/// <inheritdoc />
	public override string ToString() => $"Timer({Id})";
}
