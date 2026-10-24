namespace Bezoro.Events.Abstractions;

/// <summary>
///     Event bus supporting an explicit budget for queued dispatch.
/// </summary>
public interface IBoundedEventBus : IEventBus
{
	/// <summary>
	///     Dequeues at most <paramref name="maxEvents" /> events, including events without handlers
	///     or with failed dispatch. Callback-enqueued events can consume the remaining budget.
	///     Nested flushes have independent budgets and counts; this does not limit elapsed time or handler work.
	/// </summary>
	/// <returns>The number of events directly dequeued by this call.</returns>
	/// <exception cref="ObjectDisposedException">The bus is disposed, regardless of the budget.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvents" /> is negative.</exception>
	int FlushQueued(int maxEvents);
}
