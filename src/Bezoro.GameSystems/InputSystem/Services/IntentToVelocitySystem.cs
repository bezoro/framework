using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.InputSystem.Types;
using Bezoro.GameSystems.MovementSystem.Types;

namespace Bezoro.GameSystems.InputSystem.Services;

/// <summary>
///     Converts held movement intent into per-entity velocity for movement simulation.
/// </summary>
[Reads<InputControl>]
[Reads<MovementInputSettings>]
[Writes<MovementIntent>]
[Writes<Velocity>]
public sealed class IntentToVelocitySystem : ISystem
{
	public Stage Stage => Stage.PreTick;

	public SystemLoopPhase LoopPhase => SystemLoopPhase.FixedTick;

	/// <inheritdoc />
	public void Update(IWorld world, in SystemContext context)
	{
		ref var queue = ref world.GetResource<InputCommandQueue>();
		var now       = queue.SimulationTimeSeconds;

		foreach (var chunk in world.Query()
		             .All<InputControl>()
		             .All<MovementIntent>()
		             .All<MovementInputSettings>()
		             .All<Velocity>())
		{
			var controls   = chunk.Components<InputControl>();
			var intents    = chunk.Components<MovementIntent>();
			var settings   = chunk.Components<MovementInputSettings>();
			var velocities = chunk.Components<Velocity>();

			for (var i = 0; i < chunk.Count; i++)
			{
				var control  = controls[i];
				var setting  = settings[i];
				ref var intent = ref intents[i];
				ref var velocity = ref velocities[i];

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
	}
}
