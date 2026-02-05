using Bezoro.ECS.Abstractions;

namespace Bezoro.GameSystems.MovementSystem.Types;

/// <summary>
///     Velocity component for ECS movement.
/// </summary>
public struct Velocity : IComponent
{
	/// <summary>
	///     Gets or sets the X velocity.
	/// </summary>
	public float X;

	/// <summary>
	///     Gets or sets the Y velocity.
	/// </summary>
	public float Y;

	/// <summary>
	///     Gets or sets the Z velocity.
	/// </summary>
	public float Z;
}
