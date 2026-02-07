namespace Bezoro.ECS.Types;

/// <summary>
///     Defines the host loop phase that dispatches a system update.
/// </summary>
public enum SystemLoopPhase
{
	/// <summary>
	///     Executes during the host's regular update loop.
	/// </summary>
	Update,
	/// <summary>
	///     Executes during the host's fixed-timestep update loop.
	/// </summary>
	FixedUpdate,
	/// <summary>
	///     Executes during the host's late update loop.
	/// </summary>
	LateUpdate
}
