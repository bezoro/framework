using System.Collections.Generic;

namespace Bezoro.Core.ECS;

/// <summary>
/// Manages the registration and update cycle of systems within the Entity Component System (ECS) framework.
/// </summary>
public class SystemManager
{
	/// <summary>
	/// The list of registered systems to be managed.
	/// </summary>
	private readonly List<ISystem> systems = new();

	/// <summary>
	/// Registers a new system to be managed and updated.
	/// </summary>
	/// <param name="system">The system to register.</param>
	public void RegisterSystem(ISystem system) =>
		systems.Add(system);

	/// <summary>
	/// Invokes the update logic of all registered systems.
	/// Typically called once per frame or simulation tick.
	/// </summary>
	public void UpdateAll()
	{
		foreach (var system in systems)
		{
			system.Update();
		}
	}
}
