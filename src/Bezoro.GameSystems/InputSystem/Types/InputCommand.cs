using System;

namespace Bezoro.GameSystems.InputSystem.Types;

/// <summary>
///     Immutable movement input command produced by an external source.
/// </summary>
/// <remarks>
///     Higher <see cref="Sequence" /> values supersede older commands for the same control.
/// </remarks>
public readonly struct InputCommand
{
	/// <summary>
	///     Initializes a new instance of the <see cref="InputCommand" /> struct.
	/// </summary>
	/// <param name="controlId">Logical control channel identifier.</param>
	/// <param name="moveX">Commanded X movement component.</param>
	/// <param name="moveY">Commanded Y movement component.</param>
	/// <param name="moveZ">Commanded Z movement component.</param>
	/// <param name="sequence">Monotonic sequence number for ordering.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="controlId" /> is negative.</exception>
	public InputCommand(int controlId, float moveX, float moveY, float moveZ, ulong sequence)
	{
		if (controlId < 0)
			throw new ArgumentOutOfRangeException(nameof(controlId), "Control id must be non-negative.");

		ControlId = controlId;
		MoveX     = moveX;
		MoveY     = moveY;
		MoveZ     = moveZ;
		Sequence  = sequence;
	}

	/// <summary>
	///     Gets the logical control channel identifier.
	/// </summary>
	public int ControlId { get; }

	/// <summary>
	///     Gets the commanded X movement component.
	/// </summary>
	public float MoveX { get; }

	/// <summary>
	///     Gets the commanded Y movement component.
	/// </summary>
	public float MoveY { get; }

	/// <summary>
	///     Gets the commanded Z movement component.
	/// </summary>
	public float MoveZ { get; }

	/// <summary>
	///     Gets the monotonic sequence number for ordering.
	/// </summary>
	public ulong Sequence { get; }
}
