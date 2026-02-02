using System.Threading;

namespace Bezoro.GameSystems.ActivationSystem.Types;

/// <summary>
///     Configuration for the activation service's background processing loop.
/// </summary>
public readonly struct ActivationConfig(
	double                  timeBudgetMs     = 2.0,
	int                     iterationDelayMs = 16,
	int                     minBatchSize     = 1,
	int                     maxBatchSize     = int.MaxValue,
	SynchronizationContext? callbackContext  = null
)
{
	/// <summary>
	///     Maximum milliseconds to spend activating items per iteration.
	///     Default is 2.0ms.
	/// </summary>
	public readonly double TimeBudgetMs = timeBudgetMs;

	/// <summary>
	///     Delay in milliseconds between processing iterations.
	///     Default is 16ms (~60 Hz).
	/// </summary>
	public readonly int IterationDelayMs = iterationDelayMs;

	/// <summary>
	///     Maximum number of items to activate per iteration.
	///     Default is <see cref="int.MaxValue" /> (no cap).
	/// </summary>
	public readonly int MaxBatchSize = maxBatchSize;

	/// <summary>
	///     Minimum number of items to activate per iteration, regardless of time budget.
	///     Default is 1.
	/// </summary>
	public readonly int MinBatchSize = minBatchSize;

	/// <summary>
	///     Optional synchronization context for marshalling callbacks.
	///     When set, activation callbacks are posted to this context (e.g. Unity main thread).
	///     When null (default), callbacks execute directly on the background thread.
	/// </summary>
	public readonly SynchronizationContext? CallbackContext = callbackContext;
}
