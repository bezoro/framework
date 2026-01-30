using System;
using System.Diagnostics;

namespace Bezoro.GameSystems.TimerSystem.Types;

/// <summary>
///     Internal mutable struct representing a timer's full state.
///     Stored in the service's ConcurrentDictionary.
/// </summary>
internal struct TimerEntry
{
	/// <summary>The Stopwatch timestamp when the timer was last started or resumed.</summary>
	public long StartTimestamp;

	/// <summary>Accumulated ticks from previous running periods (used for pause/resume).</summary>
	public long AccumulatedTicks;

	/// <summary>The total duration in Stopwatch ticks.</summary>
	public readonly long DurationTicks;

	/// <summary>Current state of the timer.</summary>
	public TimerState State;

	/// <summary>Optional callback invoked when the timer completes.</summary>
	public readonly Action<TimerHandle>? OnCompleted;

	public TimerEntry(long durationTicks, Action<TimerHandle>? onCompleted)
	{
		DurationTicks    = durationTicks;
		OnCompleted      = onCompleted;
		StartTimestamp   = Stopwatch.GetTimestamp();
		AccumulatedTicks = 0;
		State            = TimerState.Running;
	}

	/// <summary>
	///     Gets the total elapsed ticks for this timer at the given timestamp.
	/// </summary>
	public readonly long GetElapsedTicks(long currentTimestamp)
	{
		return State == TimerState.Running
			? AccumulatedTicks + (currentTimestamp - StartTimestamp)
			: AccumulatedTicks;
	}

	/// <summary>
	///     Gets whether the timer has reached or exceeded its duration.
	/// </summary>
	public readonly bool IsExpired(long currentTimestamp) => GetElapsedTicks(currentTimestamp) >= DurationTicks;
}
