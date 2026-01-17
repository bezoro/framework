namespace Bezoro.ECS.Abstractions;

/// <summary>
///     Defines the contract for a system within the Entity Component System (ECS) framework.
///     Implementations should encapsulate logic to operate on entities and their components during the update loop.
/// </summary>
public interface ISystem
{
	/// <summary>
	///     Performs the update logic for this system.
	///     This is typically called once per frame or tick of the simulation.
	/// </summary>
	void Update();
}
