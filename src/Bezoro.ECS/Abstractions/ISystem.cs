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
	///     Gets the component access requirements for this system.
	///     This allows the scheduler to run compatible systems in parallel safely.
	/// </summary>
	ComponentAccess[] Accesses => [];

	/// <summary>
	///     Gets the stage this system executes in.
	/// </summary>
	Stage Stage => Stage.Update;

	/// <summary>
	///     Gets the update settings that control how often this system runs.
	/// </summary>
	SystemUpdateSettings UpdateSettings => SystemUpdateSettings.EveryFrame;

	/// <summary>
	///     Called once when the system is added to a world.
	/// </summary>
	/// <param name="world">The owning world.</param>
	void OnCreate(World world) { }

	/// <summary>
	///     Called once when the system is removed or the world is disposed.
	/// </summary>
	/// <param name="world">The owning world.</param>
	void OnDestroy(World world) { }

	/// <summary>
	///     Performs the update logic for this system.
	/// </summary>
	/// <param name="world">The world context this system operates on.</param>
	/// <param name="context">The update context for this execution.</param>
	void Update(IWorld world, in SystemContext context);
}
