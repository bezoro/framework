using System.Threading;

namespace Bezoro.GameSystems.TimerSystem.Types;

/// <summary>
///     Configuration for the timer service's background processing loop.
/// </summary>
public readonly struct TimerConfig(
	int                     tickRateMs      = 16,
	SynchronizationContext? callbackContext = null
)
{
	/// <summary>
	///     Delay in milliseconds between tick iterations.
	///     Lower values give more precise timer completion at the cost of CPU usage.
	///     Default is 16ms (~60 Hz).
	/// </summary>
	public readonly int TickRateMs = tickRateMs;

	/// <summary>
	///     Optional synchronization context for marshalling callbacks.
	///     When set, timer callbacks are posted to this context (e.g. Unity main thread).
	///     When null (default), callbacks execute directly on the background thread.
	/// </summary>
	public readonly SynchronizationContext? CallbackContext = callbackContext;
}
