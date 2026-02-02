using System;
using Bezoro.GameSystems.ActivationSystem.Types;

namespace Bezoro.GameSystems.ActivationSystem.Abstractions;

/// <summary>
///     A thread-safe activation service that spreads object activation over time
///     using a time-budget approach on a background thread.
/// </summary>
public interface IActivationService : IDisposable
{
	/// <summary>
	///     Gets whether all pending items have been activated.
	/// </summary>
	bool IsComplete { get; }

	/// <summary>
	///     Gets whether the background processing loop is currently running.
	/// </summary>
	bool IsRunning { get; }

	/// <summary>
	///     Gets the number of entries that have been activated.
	/// </summary>
	int ActivatedCount { get; }

	/// <summary>
	///     Gets the number of entries still waiting to be activated.
	/// </summary>
	int PendingCount { get; }

	/// <summary>
	///     Registers a callback to be activated during background processing.
	/// </summary>
	/// <param name="callback">The callback to invoke when activated.</param>
	/// <param name="priority">Priority for activation order. Higher values are activated first.</param>
	/// <returns>A handle to the registered activation entry.</returns>
	ActivationHandle Register(Action callback, int priority = 0);

	/// <summary>
	///     Cancels a pending activation entry.
	/// </summary>
	/// <param name="handle">The activation entry to cancel.</param>
	/// <returns><c>true</c> if the entry was pending and is now cancelled; <c>false</c> otherwise.</returns>
	bool Cancel(ActivationHandle handle);

	/// <summary>
	///     Starts the background processing loop with the specified configuration.
	/// </summary>
	/// <param name="config">The activation service configuration.</param>
	void Start(ActivationConfig config);

	/// <summary>
	///     Stops the background processing loop.
	/// </summary>
	void Stop();

	/// <summary>
	///     Raised when all pending items have been activated.
	/// </summary>
	event Action? Completed;
}
