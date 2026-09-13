namespace Bezoro.ECS.Types;

/// <summary>
///     Defines the ordered execution stages for systems.
/// </summary>
public enum Stage
{
	/// <summary>Collects external input before simulation work begins.</summary>
	Input,

	/// <summary>Runs preparation work immediately before the main simulation tick.</summary>
	PreTick,

	/// <summary>Runs the main simulation work.</summary>
	Tick,

	/// <summary>Runs follow-up work immediately after the main simulation tick.</summary>
	PostTick,

	/// <summary>Prepares or submits state for rendering.</summary>
	Render
}
