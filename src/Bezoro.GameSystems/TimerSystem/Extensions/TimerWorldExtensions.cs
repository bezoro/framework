using System;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.TimerSystem.Types;

namespace Bezoro.GameSystems.TimerSystem.Extensions;

/// <summary>
///     Extension helpers for controlling timer entities from gameplay code.
/// </summary>
public static class TimerWorldExtensions
{
	/// <summary>
	///     Transitions a timer from stopped to running and requests a started callback.
	/// </summary>
	/// <param name="world">World containing the timer entity.</param>
	/// <param name="timerEntity">Target timer entity.</param>
	/// <returns><c>true</c> when the transition was applied.</returns>
	public static bool StartTimer(this World world, Entity timerEntity)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));
		if (!TryGetTimer(world, timerEntity, out var timer))
			return false;

		if (timer.State != TimerState.Stopped)
			return false;

		timer.State = TimerState.Running;
		timer.PendingAction = TimerPendingAction.Started;
		world.Set(timerEntity, in timer);
		return true;
	}

	/// <summary>
	///     Transitions a timer from running to paused and requests a paused callback.
	/// </summary>
	/// <param name="world">World containing the timer entity.</param>
	/// <param name="timerEntity">Target timer entity.</param>
	/// <returns><c>true</c> when the transition was applied.</returns>
	public static bool PauseTimer(this World world, Entity timerEntity)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));
		if (!TryGetTimer(world, timerEntity, out var timer))
			return false;

		if (timer.State != TimerState.Running)
			return false;

		timer.State = TimerState.Paused;
		timer.PendingAction = TimerPendingAction.Paused;
		world.Set(timerEntity, in timer);
		return true;
	}

	/// <summary>
	///     Transitions a timer to stopped and requests a stopped callback.
	/// </summary>
	/// <param name="world">World containing the timer entity.</param>
	/// <param name="timerEntity">Target timer entity.</param>
	/// <returns><c>true</c> when the transition was applied.</returns>
	public static bool StopTimer(this World world, Entity timerEntity)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));
		if (!TryGetTimer(world, timerEntity, out var timer))
			return false;

		if (timer.State is not (TimerState.Running or TimerState.Paused))
			return false;

		timer.State = TimerState.Stopped;
		timer.PendingAction = TimerPendingAction.Stopped;
		world.Set(timerEntity, in timer);
		return true;
	}

	/// <summary>
	///     Transitions a timer from paused to running and requests a resumed callback.
	/// </summary>
	/// <param name="world">World containing the timer entity.</param>
	/// <param name="timerEntity">Target timer entity.</param>
	/// <returns><c>true</c> when the transition was applied.</returns>
	public static bool ResumeTimer(this World world, Entity timerEntity)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));
		if (!TryGetTimer(world, timerEntity, out var timer))
			return false;

		if (timer.State != TimerState.Paused)
			return false;

		timer.State = TimerState.Running;
		timer.PendingAction = TimerPendingAction.Resumed;
		world.Set(timerEntity, in timer);
		return true;
	}

	/// <summary>
	///     Resets elapsed time to zero, starts running, and requests a restarted callback.
	/// </summary>
	/// <param name="world">World containing the timer entity.</param>
	/// <param name="timerEntity">Target timer entity.</param>
	/// <returns><c>true</c> when the transition was applied.</returns>
	public static bool RestartTimer(this World world, Entity timerEntity)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));
		if (!TryGetTimer(world, timerEntity, out var timer))
			return false;

		timer.ElapsedSeconds = 0f;
		timer.State = TimerState.Running;
		timer.PendingAction = TimerPendingAction.Restarted;
		world.Set(timerEntity, in timer);
		return true;
	}

	private static bool TryGetTimer(World world, Entity timerEntity, out Timer timer)
	{
		if (!world.IsAlive(timerEntity))
		{
			timer = default;
			return false;
		}

		return world.TryGet(timerEntity, out timer);
	}
}
