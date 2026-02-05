using Bezoro.ECS.Abstractions;

namespace Bezoro.GameSystems.MovementSystem.Types;

/// <summary>
///     Position component for ECS movement.
/// </summary>
public struct Position : IComponent
{
	/// <summary>
	///     Gets or sets the X coordinate.
	/// </summary>
	public float X;

	/// <summary>
	///     Gets or sets the Y coordinate.
	/// </summary>
	public float Y;

	/// <summary>
	///     Gets or sets the Z coordinate.
	/// </summary>
	public float Z;
}
