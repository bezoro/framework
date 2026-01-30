using System;
using System.Diagnostics;

namespace Bezoro.GameSystems.TimerSystem.Types;

/// <summary>
///     Internal mutable struct representing a timer's full state.
///     Stored in the service's ConcurrentDictionary.
/// </summary>
internal record struct TimerEntry
{
	/// <summary>Optional callback invoked when the timer completes.</summary>
	public readonly Action<TimerHandle>? OnCompleted;

	/// <summary>The total duration in Stopwatch ticks.</summary>
	public readonly long DurationTicks;

	/// <summary>The lifecycle mode of this timer.</summary>
	public readonly TimerMode Mode;

	/// <summary>Accumulated ticks from previous running periods (used for pause/resume).</summary>
	public long AccumulatedTicks;
	/// <summary>The Stopwatch timestamp when the timer was last started or resumed.</summary>
	public long StartTimestamp;

	/// <summary>Current state of the timer.</summary>
	public TimerState State;

	public TimerEntry(long durationTicks, Action<TimerHandle>? onCompleted, TimerMode mode = TimerMode.OneShot)
	{
		DurationTicks    = durationTicks;
		OnCompleted      = onCompleted;
		Mode             = mode;
		StartTimestamp   = Stopwatch.GetTimestamp();
		AccumulatedTicks = 0;
		State            = TimerState.Running;
	}

	/// <summary>
	///     Gets whether the timer has reached or exceeded its duration.
	/// </summary>
	public readonly bool IsExpired(long currentTimestamp) => GetElapsedTicks(currentTimestamp) >= DurationTicks;

	/// <summary>
	///     Gets the total elapsed ticks for this timer at the given timestamp.
	/// </summary>
	public readonly long GetElapsedTicks(long currentTimestamp) =>
		State == TimerState.Running
			? AccumulatedTicks + (currentTimestamp - StartTimestamp)
			: AccumulatedTicks;
}
