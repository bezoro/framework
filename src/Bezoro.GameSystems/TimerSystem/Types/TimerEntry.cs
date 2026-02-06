using System;
using System.Diagnostics;

namespace Bezoro.GameSystems.TimerSystem.Types;

/// <summary>
///     Internal mutable struct representing a timer's full state.
///     Stored in the service's ConcurrentDictionary.
/// </summary>
internal record struct TimerEntry(
	long                 DurationTicks,
	Action<TimerHandle>? OnCompleted,
	TimerMode            Mode = TimerMode.OneShot
)
{
	/// <summary>Optional callback invoked when the timer completes.</summary>
	public readonly Action<TimerHandle>? OnCompleted = OnCompleted;

	/// <summary>The total duration in Stopwatch ticks.</summary>
	public readonly long DurationTicks = DurationTicks;

	/// <summary>The lifecycle mode of this timer.</summary>
	public readonly TimerMode Mode = Mode;

	/// <summary>Accumulated ticks from previous running periods (used for pause/resume).</summary>
	public long AccumulatedTicks = 0;
	/// <summary>The Stopwatch timestamp when the timer was last started or resumed.</summary>
	public long StartTimestamp = Stopwatch.GetTimestamp();

	/// <summary>Current state of the timer.</summary>
	public TimerState State = TimerState.Running;

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
