using System;
using Bezoro.ECS.Types;

namespace Bezoro.GameSystems.TimerSystem.Types;

/// <summary>
///     Payload describing a timer lifecycle transition.
/// </summary>
public readonly struct TimerLifecycleEvent
{
	/// <summary>
	///     Initializes a new lifecycle event payload.
	/// </summary>
	public TimerLifecycleEvent(
		Entity         timerEntity,
		Entity         ownerEntity,
		int            timerId,
		TimerLifecycle lifecycle,
		TimerState     state,
		float          elapsedSeconds,
		float          durationSeconds)
	{
		TimerEntity = timerEntity;
		OwnerEntity = ownerEntity;
		TimerId = timerId;
		Lifecycle = lifecycle;
		State = state;
		ElapsedSeconds = elapsedSeconds;
		DurationSeconds = durationSeconds;
	}

	/// <summary>
	///     Gets the timer entity that transitioned.
	/// </summary>
	public Entity TimerEntity { get; }

	/// <summary>
	///     Gets the owner entity if present; otherwise <see cref="Entity.None" />.
	/// </summary>
	public Entity OwnerEntity { get; }

	/// <summary>
	///     Gets application-level timer identifier.
	/// </summary>
	public int TimerId { get; }

	/// <summary>
	///     Gets transition type.
	/// </summary>
	public TimerLifecycle Lifecycle { get; }

	/// <summary>
	///     Gets timer state after transition.
	/// </summary>
	public TimerState State { get; }

	/// <summary>
	///     Gets elapsed time in seconds after transition.
	/// </summary>
	public float ElapsedSeconds { get; }

	/// <summary>
	///     Gets configured total duration in seconds.
	/// </summary>
	public float DurationSeconds { get; }

	/// <summary>
	///     Gets completion progress from 0 to 1.
	/// </summary>
	public float Progress => DurationSeconds <= 0f ? 1f : Math.Min(1f, ElapsedSeconds / DurationSeconds);
}
