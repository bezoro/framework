using Bezoro.ECS.Types;

namespace Bezoro.ECS.Abstractions;

/// <summary>
///     Defines the contract for a system within the Entity Component System (ECS) framework.
///     Implementations should encapsulate logic to operate on entities and their components during the update loop.
/// </summary>
public interface ISystem
{
	/// <summary>
	///     Gets the update settings that control how often this system runs.
	/// </summary>
	SystemUpdateSettings UpdateSettings { get; }

	/// <summary>
	///     Gets the component access requirements for this system.
	///     This allows the scheduler to run compatible systems in parallel safely.
	/// </summary>
	ComponentAccess[] Accesses { get; }

	/// <summary>
	///     Performs the update logic for this system.
	/// </summary>
	/// <param name="world">The world context this system operates on.</param>
	/// <param name="context">The update context for this execution.</param>
	void Update(IWorld world, in SystemContext context);
}
