using System;
using System.Collections.Generic;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.InputSystem.Types;

namespace Bezoro.GameSystems.InputSystem.Services;

/// <summary>
///     Drains external input commands into latest-per-control snapshots each fixed update.
/// </summary>
public sealed class InputIngestionSystem : ISystem
{
	public Stage Stage => Stage.Input;

	public SystemLoopPhase LoopPhase => SystemLoopPhase.FixedTick;

	public void OnCreate(World world)
	{
		if (world is null)
			throw new ArgumentNullException(nameof(world));

		try
		{
			_ = world.GetResource<InputCommandQueue>();
		}
		catch (KeyNotFoundException)
		{
			world.SetResource(new InputCommandQueue());
		}
	}

	/// <inheritdoc />
	public void Update(IWorld world, in SystemContext context)
	{
		ref var queue = ref world.GetResource<InputCommandQueue>();
		queue.AdvanceTime(context.DeltaTime);
		queue.Drain();
	}
}
