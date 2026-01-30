namespace Bezoro.GameSystems.TimerSystem.Types;

/// <summary>
///     Event arguments for the global timer completed event.
/// </summary>
public readonly struct TimerCompletedEventArgs
{
	/// <summary>The handle of the timer that completed.</summary>
	public readonly TimerHandle Handle;

	/// <summary>
	///     Creates event arguments for a completed timer.
	/// </summary>
	/// <param name="handle">The handle of the completed timer.</param>
	public TimerCompletedEventArgs(TimerHandle handle) => Handle = handle;
}
