using System;
using Bezoro.GameSystems.TimerSystem.Types;

namespace Bezoro.GameSystems.TimerSystem.Abstractions;

/// <summary>
///     A high-performance, thread-safe timer service for managing cooldowns and timed events.
/// </summary>
public interface ITimerService : IDisposable
{
	/// <summary>
	///     Gets whether the background processing loop is currently running.
	/// </summary>
	bool IsRunning { get; }

	/// <summary>
	///     Gets the number of active timers (Running or Paused).
	/// </summary>
	int ActiveCount { get; }

	/// <summary>
	///     Cancels a timer, setting its state to Stopped.
	/// </summary>
	/// <param name="handle">The timer to cancel.</param>
	/// <returns><c>true</c> if the timer was active and is now stopped; <c>false</c> otherwise.</returns>
	bool Cancel(TimerHandle handle);

	/// <summary>
	///     Pauses a running timer, preserving its elapsed time.
	/// </summary>
	/// <param name="handle">The timer to pause.</param>
	/// <returns><c>true</c> if the timer was running and is now paused; <c>false</c> otherwise.</returns>
	bool Pause(TimerHandle handle);

	/// <summary>
	///     Restarts a timer from the beginning, resetting elapsed time to zero.
	///     Works from any state (Running, Paused, Stopped, Completed).
	/// </summary>
	/// <param name="handle">The timer to restart.</param>
	/// <returns><c>true</c> if the timer exists and was restarted; <c>false</c> otherwise.</returns>
	bool Restart(TimerHandle handle);

	/// <summary>
	///     Resumes a paused timer from where it left off.
	/// </summary>
	/// <param name="handle">The timer to resume.</param>
	/// <returns><c>true</c> if the timer was paused and is now running; <c>false</c> otherwise.</returns>
	bool Resume(TimerHandle handle);

	/// <summary>
	///     Queries the current state of a timer.
	/// </summary>
	/// <param name="handle">The timer to query.</param>
	/// <param name="info">The timer's current state snapshot, if found.</param>
	/// <returns><c>true</c> if the timer exists; <c>false</c> otherwise.</returns>
	bool TryGetInfo(TimerHandle handle, out TimerInfo info);

	/// <summary>
	///     Removes all completed and stopped timer entries.
	/// </summary>
	/// <returns>The number of entries removed.</returns>
	int Cleanup();

	/// <summary>
	///     Creates a new timer with the specified duration.
	/// </summary>
	/// <param name="duration">The timer duration. Must be positive.</param>
	/// <param name="onCompleted">Optional callback invoked when the timer completes.</param>
	/// <param name="mode">
	///     The timer lifecycle mode. <see cref="TimerMode.OneShot" /> timers are auto-removed after
	///     the completion callback fires. <see cref="TimerMode.Persistent" /> timers stay in storage
	///     for reuse via <see cref="Restart" />. Defaults to <see cref="TimerMode.OneShot" />.
	/// </param>
	/// <returns>A handle to the created timer.</returns>
	TimerHandle Create(TimeSpan duration, Action<TimerHandle>? onCompleted = null, TimerMode mode = TimerMode.OneShot);

	/// <summary>
	///     Starts the background processing loop with the specified configuration.
	/// </summary>
	/// <param name="config">The timer service configuration.</param>
	void Start(TimerConfig config);

	/// <summary>
	///     Stops the background processing loop.
	/// </summary>
	void Stop();

	/// <summary>
	///     Raised when any timer completes. Useful for centralized monitoring.
	/// </summary>
	event Action<TimerCompletedEventArgs>? TimerCompleted;
}
