using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Abstractions;

/// <summary>
///     Defines the contract for a system within the Entity Component System (ECS) framework.
///     Implementations should encapsulate logic to operate on entities and their components during the update loop.
/// </summary>
public interface ISystem
{
	/// <summary>
	///     Gets the stage this system executes in.
	/// </summary>
	Stage Stage => Stage.Tick;

	/// <summary>
	///     Gets the host loop phase this system executes in.
	/// </summary>
	SystemLoopPhase LoopPhase => SystemLoopPhase.Tick;

	/// <summary>
	///     Gets the update settings that control how often this system runs.
	/// </summary>
	SystemUpdateSettings UpdateSettings => SystemUpdateSettings.EveryTick;

	/// <summary>
	///     Called once when the system is added to a world.
	/// </summary>
	/// <param name="world">The owning world.</param>
	void OnCreate(WorldV1 world) { }

	/// <summary>
	///     Called once when the system is removed or the world is disposed.
	/// </summary>
	/// <param name="world">The owning world.</param>
	void OnDestroy(WorldV1 world) { }

	/// <summary>
	///     Performs the update logic for this system.
	/// </summary>
	/// <param name="world">The world context this system operates on.</param>
	/// <param name="context">The update context for this execution.</param>
	void Update(IWorld world, in SystemContext context);
}
