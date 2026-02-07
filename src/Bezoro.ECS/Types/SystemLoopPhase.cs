namespace Bezoro.ECS.Types;

/// <summary>
///     Defines the host loop phase that dispatches a system update.
/// </summary>
public enum SystemLoopPhase
{
	/// <summary>
	///     Executes during the host's regular tick loop.
	/// </summary>
	Tick,
	/// <summary>
	///     Executes during the host's fixed-timestep tick loop.
	/// </summary>
	FixedTick,
	/// <summary>
	///     Executes during the host's late tick loop.
	/// </summary>
	LateTick
}
