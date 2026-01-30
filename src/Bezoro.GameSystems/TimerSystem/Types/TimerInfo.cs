using System;
using System.Diagnostics;

namespace Bezoro.GameSystems.TimerSystem.Types;

/// <summary>
///     A readonly snapshot of a timer's current state, suitable for UI queries.
/// </summary>
public readonly struct TimerInfo
{
	/// <summary>
	///     The completion progress from 0.0 (just started) to 1.0 (completed).
	/// </summary>
	public readonly double Progress;
	/// <summary>The handle identifying this timer.</summary>
	public readonly TimerHandle Handle;

	/// <summary>The current state of the timer.</summary>
	public readonly TimerState State;

	/// <summary>The total duration of the timer.</summary>
	public readonly TimeSpan Duration;

	/// <summary>The time elapsed so far.</summary>
	public readonly TimeSpan Elapsed;

	/// <summary>The time remaining before completion.</summary>
	public readonly TimeSpan Remaining;

	internal TimerInfo(TimerHandle handle, TimerState state, long durationTicks, long elapsedTicks)
	{
		Handle   = handle;
		State    = state;
		Duration = TicksToTimeSpan(durationTicks);
		Elapsed  = TicksToTimeSpan(Math.Min(elapsedTicks, durationTicks));

		long remainingTicks = Math.Max(0, durationTicks - elapsedTicks);
		Remaining = TicksToTimeSpan(remainingTicks);

		Progress = durationTicks > 0
					   ? Math.Min(1.0, (double)elapsedTicks / durationTicks)
					   : 1.0;
	}

	private static TimeSpan TicksToTimeSpan(long stopwatchTicks)
	{
		double seconds = (double)stopwatchTicks / Stopwatch.Frequency;
		return TimeSpan.FromSeconds(seconds);
	}
}
