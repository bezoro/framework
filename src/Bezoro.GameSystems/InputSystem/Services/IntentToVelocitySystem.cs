using System;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.InputSystem.Types;
using Bezoro.GameSystems.MovementSystem.Types;

namespace Bezoro.GameSystems.InputSystem.Services;

/// <summary>
///     Converts held movement intent into per-entity velocity for movement simulation.
/// </summary>
[Reads(typeof(InputControl))]
[Reads(typeof(MovementInputSettings))]
[Writes(typeof(MovementIntent))]
[Writes(typeof(Velocity))]
public sealed class IntentToVelocitySystem : ISystem
{
	private QueryHandle<IntentToVelocityQuerySpec> _query;

	public Stage Stage => Stage.PreTick;

	public SystemLoopPhase LoopPhase => SystemLoopPhase.FixedTick;

	public void OnCreate(World world)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));
		_query = world.Compile<IntentToVelocityQuerySpec>();
	}

	/// <inheritdoc />
	public void Update(in SystemContext context)
	{
		var world = context.World;
		ref var queue = ref world.GetResource<InputCommandQueue>();
		var now       = queue.SimulationTimeSeconds;

		using var cursor = world.Execute(_query);
		if (!cursor.MoveNext())
			return;

		var entities = cursor.Current;
		for (var i = 0; i < entities.Length; i++)
		{
			var control = cursor.Get<InputControl>(i);
			var setting = cursor.Get<MovementInputSettings>(i);
			ref var intent = ref cursor.Get<MovementIntent>(i);
			ref var velocity = ref cursor.Get<Velocity>(i);

			if (!queue.TryGetLatest(control.ControlId, out var state))
			{
				intent   = default;
				velocity = default;
				continue;
			}

			var ageSeconds = now - state.ReceivedAtSeconds;
			if (ageSeconds > setting.HoldDurationSeconds)
			{
				intent   = default;
				velocity = default;
				continue;
			}

			intent.X   = state.MoveX;
			intent.Y   = state.MoveY;
			intent.Z   = state.MoveZ;
			velocity.X = state.MoveX * setting.Speed;
			velocity.Y = state.MoveY * setting.Speed;
			velocity.Z = state.MoveZ * setting.Speed;
		}
	}

	private readonly struct IntentToVelocityQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<InputControl>();
			builder.All<MovementIntent>();
			builder.All<MovementInputSettings>();
			builder.All<Velocity>();
		}
	}
}
