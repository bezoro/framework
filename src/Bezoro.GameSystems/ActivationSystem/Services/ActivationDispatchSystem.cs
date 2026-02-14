using System;
using System.Collections.Generic;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.ActivationSystem.Types;

namespace Bezoro.GameSystems.ActivationSystem.Services;

/// <summary>
///     ECS system that executes callbacks activated by <see cref="ActivationProcessingSystem" />.
/// </summary>
public sealed class ActivationDispatchSystem : ISystem
{
	public Stage Stage => Stage.PostTick;

	public SystemLoopPhase LoopPhase => SystemLoopPhase.Tick;

	/// <inheritdoc />
	public void OnCreate(WorldV1 world)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));
		EnsureResources(world);
	}

	/// <inheritdoc />
	public void Update(IWorld world, in SystemContext context)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));
		EnsureResources(world);

		ref var config = ref world.GetResource<ActivationConfig>();
		ref var dispatchQueue = ref world.GetResource<ActivationDispatchQueueResource>();
		while (dispatchQueue.TryDequeue(out var callback))
		{
			try
			{
				if (config.CallbackDispatcher is null)
				{
					callback();
					continue;
				}

				config.CallbackDispatcher(callback);
			}
			catch
			{
				// Callback and dispatcher exceptions should not break simulation.
			}
		}
	}

	private static void EnsureResources(IWorld world)
	{
		try
		{
			_ = world.GetResource<ActivationConfig>();
		}
		catch (KeyNotFoundException)
		{
			world.SetResource(new ActivationConfig());
		}

		try
		{
			_ = world.GetResource<ActivationDispatchQueueResource>();
		}
		catch (KeyNotFoundException)
		{
			world.SetResource(new ActivationDispatchQueueResource());
		}
	}
}
