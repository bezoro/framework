using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;

namespace Bezoro.ECS.Types;

/// <summary>
///     Provides context for system run-condition evaluation.
/// </summary>
/// <param name="world">World executing the current scheduler pass.</param>
/// <param name="system">System currently being evaluated.</param>
/// <param name="loopPhase">Loop phase for the current scheduler pass.</param>
/// <param name="stage">Stage for the current scheduler pass.</param>
/// <param name="deltaTime">Delta time for the current scheduler pass.</param>
public readonly struct SystemRunConditionContext(
	World           world,
	ISystem         system,
	SystemLoopPhase loopPhase,
	Stage           stage,
	float           deltaTime)
{
	/// <summary>
	///     Gets the delta time for the current scheduler pass.
	/// </summary>
	public float DeltaTime { get; } = deltaTime;

	/// <summary>
	///     Gets the loop phase for the current scheduler pass.
	/// </summary>
	public SystemLoopPhase LoopPhase { get; } = loopPhase;

	/// <summary>
	///     Gets the stage for the current scheduler pass.
	/// </summary>
	public Stage Stage { get; } = stage;

	/// <summary>
	///     Gets the system currently being evaluated.
	/// </summary>
	public ISystem System { get; } = system ?? throw new ArgumentNullException(nameof(system));

	/// <summary>
	///     Gets the world executing the current scheduler pass.
	/// </summary>
	public World World { get; } = world ?? throw new ArgumentNullException(nameof(world));
}
