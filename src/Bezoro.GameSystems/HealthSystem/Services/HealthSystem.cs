using System;
using System.Collections.Generic;
using Bezoro.Core.Types;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.HealthSystem.Types;

namespace Bezoro.GameSystems.HealthSystem.Services;

/// <summary>
///     ECS system that applies queued health mutations to health components.
/// </summary>
[Writes<Health>]
[Reads<HealthMutationRequest>]
public sealed class HealthSystem : ISystem
{
	/// <summary>
	///     Raised when a request changes health values.
	/// </summary>
	public event Action<HealthChangedEvent>? Changed;

	public Stage Stage => Stage.Tick;

	public SystemLoopPhase LoopPhase => SystemLoopPhase.Tick;

	public SystemUpdateSettings UpdateSettings => SystemUpdateSettings.EveryTick;

	/// <inheritdoc />
	public void OnCreate(WorldV1 world)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));
		EnsureEventsResource(world);
	}

	/// <inheritdoc />
	public void Update(IWorld world, in SystemContext context)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));
		EnsureEventsResource(world);

		var enumerator = world.Query().All<HealthMutationRequest>().GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				var chunk = enumerator.Current;
				var entities = chunk.Entities;
				var requests = chunk.ReadOnlyComponents<HealthMutationRequest>();
				for (var i = 0; i < chunk.Count; i++)
				{
					Entity requestEntity = entities[i];
					ref readonly var request = ref requests[i];

					ProcessRequest(world, request);
					context.Commands.DestroyEntity(requestEntity);
				}
			}
		}
		finally
		{
			enumerator.Dispose();
		}
	}

	private void ProcessRequest(IWorld world, in HealthMutationRequest request)
	{
		if (!world.IsAlive(request.TargetEntity))
			return;

		if (!world.TryGet(request.TargetEntity, out Health health))
			return;

		var oldCurrent = health.Current;
		var oldMax = health.Max;
		var oldExcess = health.ExcessCurrent;

		var kind = ApplyMutation(ref health, in request);
		if (oldCurrent == health.Current && oldMax == health.Max && oldExcess == health.ExcessCurrent)
			return;

		world.Set(request.TargetEntity, in health);

		var eventData = new HealthChangedEvent(
			request.TargetEntity,
			kind,
			request.Value,
			request.MaxUpdateMode,
			oldCurrent,
			oldMax,
			oldExcess,
			health.Current,
			health.Max,
			health.ExcessCurrent
		);

		ref var events = ref world.GetResource<HealthEventsResource>();
		events.Enqueue(in eventData);

		try
		{
			Changed?.Invoke(eventData);
		}
		catch
		{
			// Event handler exceptions should not break simulation.
		}
	}

	private static HealthChangeKind ApplyMutation(ref Health health, in HealthMutationRequest request) =>
		request.Kind switch
		{
			HealthMutationKind.Damage => ApplyDamage(ref health, request.Value),
			HealthMutationKind.Heal => ApplyHeal(ref health, request.Value),
			HealthMutationKind.DirectDamage => ApplyDirectDamage(ref health, request.Value),
			HealthMutationKind.IncreaseHealth => ApplyIncreaseHealth(ref health, request.Value),
			HealthMutationKind.SetMax => ApplySetMax(ref health, request.Value, request.MaxUpdateMode),
			_ => throw new ArgumentOutOfRangeException(nameof(request), request.Kind, "Unsupported mutation kind.")
		};

	private static HealthChangeKind ApplyDamage(ref Health health, uint amount)
	{
		if (amount == 0u)
			return HealthChangeKind.Damage;

		uint remaining = amount;
		if (health.ExcessCurrent > 0u)
		{
			uint absorbedByExcess = remaining <= health.ExcessCurrent ? remaining : health.ExcessCurrent;
			health.ExcessCurrent -= absorbedByExcess;
			remaining -= absorbedByExcess;
		}

		if (remaining > 0u)
			health.Current = remaining >= health.Current ? 0u : health.Current - remaining;

		return HealthChangeKind.Damage;
	}

	private static HealthChangeKind ApplyHeal(ref Health health, uint amount)
	{
		if (amount == 0u)
			return HealthChangeKind.Heal;

		uint roomInCurrent = health.Max > health.Current ? health.Max - health.Current : 0u;
		uint healToCurrent = amount <= roomInCurrent ? amount : roomInCurrent;
		health.Current += healToCurrent;
		return HealthChangeKind.Heal;
	}

	private static HealthChangeKind ApplyDirectDamage(ref Health health, uint amount)
	{
		if (amount == 0u)
			return HealthChangeKind.DirectDamage;

		health.Current = amount >= health.Current ? 0u : health.Current - amount;
		return HealthChangeKind.DirectDamage;
	}

	private static HealthChangeKind ApplyIncreaseHealth(ref Health health, uint amount)
	{
		if (amount == 0u)
			return HealthChangeKind.IncreaseHealth;

		uint roomInCurrent = health.Max > health.Current ? health.Max - health.Current : 0u;
		uint healToCurrent = amount <= roomInCurrent ? amount : roomInCurrent;
		health.Current += healToCurrent;

		uint overflow = amount - healToCurrent;
		if (overflow == 0u || health.ExcessMax == 0u)
			return HealthChangeKind.IncreaseHealth;

		uint roomInExcess = health.ExcessMax > health.ExcessCurrent ? health.ExcessMax - health.ExcessCurrent : 0u;
		uint healToExcess = overflow <= roomInExcess ? overflow : roomInExcess;
		health.ExcessCurrent += healToExcess;
		return HealthChangeKind.IncreaseHealth;
	}

	private static HealthChangeKind ApplySetMax(ref Health health, uint max, MaxValueUpdateMode mode)
	{
		uint oldMax = health.Max;
		uint oldCurrent = health.Current;
		health.Max = max;
		health.Current = mode switch
		{
			MaxValueUpdateMode.ClampCurrent => oldCurrent > max ? max : oldCurrent,
			MaxValueUpdateMode.PreservePercentage => ScaleCurrent(oldCurrent, oldMax, max),
			_ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid max update mode.")
		};

		return HealthChangeKind.SetMax;
	}

	private static uint ScaleCurrent(uint current, uint oldMax, uint newMax)
	{
		if (oldMax == 0u)
			return current > newMax ? newMax : current;

		ulong numerator = (ulong)current * newMax + oldMax / 2u;
		uint scaled = (uint)(numerator / oldMax);
		return scaled > newMax ? newMax : scaled;
	}

	private static void EnsureEventsResource(IWorld world)
	{
		try
		{
			_ = world.GetResource<HealthEventsResource>();
		}
		catch (KeyNotFoundException)
		{
			world.SetResource(new HealthEventsResource());
		}
	}
}
