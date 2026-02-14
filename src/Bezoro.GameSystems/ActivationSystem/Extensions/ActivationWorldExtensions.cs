using System;
using System.Collections.Generic;
using Bezoro.ECS.Services;
using Bezoro.GameSystems.ActivationSystem.Services;
using Bezoro.GameSystems.ActivationSystem.Types;

namespace Bezoro.GameSystems.ActivationSystem.Extensions;

/// <summary>
///     Helpers for wiring activation resources and systems into an ECS world.
/// </summary>
public static class ActivationWorldExtensions
{
	/// <summary>
	///     Gets the existing <see cref="ActivationCommandQueue" /> resource or creates one if missing.
	/// </summary>
	/// <param name="world">The ECS world.</param>
	/// <returns>The activation command queue resource.</returns>
	public static ActivationCommandQueue GetOrCreateActivationCommandQueue(this WorldV1 world)
	{
		if (world is null)
			throw new ArgumentNullException(nameof(world));

		try
		{
			return world.GetResource<ActivationCommandQueue>();
		}
		catch (KeyNotFoundException)
		{
			var queue = new ActivationCommandQueue();
			world.SetResource(queue);
			return queue;
		}
	}

	/// <summary>
	///     Registers activation systems with their declared stage ordering.
	/// </summary>
	/// <param name="world">The ECS world.</param>
	public static void AddActivationPipeline(this WorldV1 world)
	{
		if (world is null)
			throw new ArgumentNullException(nameof(world));

		var ingestionSystem = new ActivationIngestionSystem();
		var processingSystem = new ActivationProcessingSystem();
		var dispatchSystem = new ActivationDispatchSystem();

		world.AddSystem(ingestionSystem, ingestionSystem.Stage);
		world.AddSystem(processingSystem, processingSystem.Stage);
		world.AddSystem(dispatchSystem, dispatchSystem.Stage);
	}
}
