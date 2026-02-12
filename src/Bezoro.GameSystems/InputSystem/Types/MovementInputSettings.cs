namespace Bezoro.GameSystems.InputSystem.Types;

/// <summary>
///     Per-entity settings for converting movement intent into velocity.
/// </summary>
public struct MovementInputSettings
{
	/// <summary>
	///     Gets or sets the movement speed multiplier.
	/// </summary>
	public float Speed;

	/// <summary>
	///     Gets or sets how long the most recent input is kept when no new command arrives.
	/// </summary>
	public float HoldDurationSeconds;
}
