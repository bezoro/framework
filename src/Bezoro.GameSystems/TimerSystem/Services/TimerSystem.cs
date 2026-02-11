using System;
using System.Collections.Generic;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.TimerSystem.Types;

namespace Bezoro.GameSystems.TimerSystem.Services;

/// <summary>
///     ECS system that advances running timers and emits lifecycle callbacks.
/// </summary>
[Writes<Timer>]
public sealed class TimerSystem : ISystem
{
	/// <summary>
	///     Raised when a timer transitions to running from stopped.
	/// </summary>
	public event Action<TimerLifecycleEvent>? Started;

	/// <summary>
	///     Raised when a timer transitions to paused.
	/// </summary>
	public event Action<TimerLifecycleEvent>? Paused;

	/// <summary>
	///     Raised when a timer transitions to stopped.
	/// </summary>
	public event Action<TimerLifecycleEvent>? Stopped;

	/// <summary>
	///     Raised when a timer reaches its duration.
	/// </summary>
	public event Action<TimerLifecycleEvent>? Finished;

	/// <summary>
	///     Raised when a paused timer resumes running.
	/// </summary>
	public event Action<TimerLifecycleEvent>? Resumed;

	/// <summary>
	///     Raised when a timer is explicitly restarted.
	/// </summary>
	public event Action<TimerLifecycleEvent>? Restarted;

	public Stage Stage => Stage.Tick;

	public SystemLoopPhase LoopPhase => SystemLoopPhase.Tick;

	public SystemUpdateSettings UpdateSettings => SystemUpdateSettings.EveryTick;

	/// <inheritdoc />
	public void OnCreate(World world)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));
		EnsureEventsResource(world);
	}

	/// <inheritdoc />
	public void Update(IWorld world, in SystemContext context)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));

		EnsureEventsResource(world);
		float deltaTime = context.DeltaTime <= 0f ? 0f : context.DeltaTime;

		var enumerator = world.Query().All<Timer>().GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				var chunk = enumerator.Current;
				var entities = chunk.Entities;
				var timers = chunk.Components<Timer>();

				for (var i = 0; i < chunk.Count; i++)
				{
					ref var timer = ref timers[i];
					var timerEntity = entities[i];

					PublishPendingTransition(world, timerEntity, ref timer);
					AdvanceTimer(world, context.Commands, timerEntity, ref timer, deltaTime);
				}
			}
		}
		finally
		{
			enumerator.Dispose();
		}
	}

	private void AdvanceTimer(
		IWorld        world,
		CommandBuffer commands,
		Entity        timerEntity,
		ref Timer     timer,
		float         deltaTime)
	{
		if (timer.State != TimerState.Running || deltaTime <= 0f)
			return;

		timer.ElapsedSeconds += deltaTime;
		if (timer.ElapsedSeconds < timer.DurationSeconds)
			return;

		timer.ElapsedSeconds = timer.DurationSeconds;
		timer.State = TimerState.Completed;

		Publish(world, timerEntity, timer, TimerLifecycle.Finished);

		if (timer.Mode == TimerMode.OneShot)
			commands.DestroyEntity(timerEntity);
	}

	private void PublishPendingTransition(
		IWorld     world,
		Entity     timerEntity,
		ref Timer  timer)
	{
		var lifecycle = timer.PendingAction switch
		{
			TimerPendingAction.Started => TimerLifecycle.Started,
			TimerPendingAction.Paused => TimerLifecycle.Paused,
			TimerPendingAction.Stopped => TimerLifecycle.Stopped,
			TimerPendingAction.Resumed => TimerLifecycle.Resumed,
			TimerPendingAction.Restarted => TimerLifecycle.Restarted,
			_ => (TimerLifecycle?)null
		};

		timer.PendingAction = TimerPendingAction.None;
		if (!lifecycle.HasValue)
			return;

		Publish(world, timerEntity, timer, lifecycle.Value);
	}

	private void Publish(
		IWorld         world,
		Entity         timerEntity,
		in Timer       timer,
		TimerLifecycle lifecycle)
	{
		var ownerEntity = TryResolveOwner(world, timerEntity);
		var eventData = new TimerLifecycleEvent(
			timerEntity,
			ownerEntity,
			timer.TimerId,
			lifecycle,
			timer.State,
			timer.ElapsedSeconds,
			timer.DurationSeconds
		);

		ref var events = ref world.GetResource<TimerEventsResource>();
		events.Enqueue(in eventData);

		try
		{
			GetHandler(lifecycle)?.Invoke(eventData);
		}
		catch
		{
			// Event handler exceptions should not break simulation.
		}
	}

	private Action<TimerLifecycleEvent>? GetHandler(TimerLifecycle lifecycle) =>
		lifecycle switch
		{
			TimerLifecycle.Started => Started,
			TimerLifecycle.Paused => Paused,
			TimerLifecycle.Stopped => Stopped,
			TimerLifecycle.Finished => Finished,
			TimerLifecycle.Resumed => Resumed,
			TimerLifecycle.Restarted => Restarted,
			_ => null
		};

	private static Entity TryResolveOwner(IWorld world, Entity timerEntity)
	{
		if (world.TryGet(timerEntity, out TimerOwner owner))
			return owner.OwnerEntity;

		return Entity.None;
	}

	private static void EnsureEventsResource(IWorld world)
	{
		try
		{
			_ = world.GetResource<TimerEventsResource>();
		}
		catch (KeyNotFoundException)
		{
			world.SetResource(new TimerEventsResource());
		}
	}
}
