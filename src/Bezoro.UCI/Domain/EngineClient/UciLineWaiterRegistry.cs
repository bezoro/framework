using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Bezoro.UCI.Domain;

/// <summary>
///     Maintains a registry of asynchronous waiters that complete when an engine output line satisfies a predicate.
///     Decouples imperative wait-points from the background read loop.
/// </summary>
internal sealed class UciLineWaiterRegistry
{
	private readonly ConcurrentDictionary<Guid, WaiterRegistration> _waiters = new();
	private          int                                            _waiterCount;

	/// <summary>
	///     True when at least one waiter is currently registered.
	/// </summary>
	public bool HasWaiters => Volatile.Read(ref _waiterCount) > 0;

	/// <summary>
	///     Registers a waiter for an output line that matches <paramref name="predicate" />.
	/// </summary>
	public async Task<string> WaitForAsync(
		Func<string, bool> predicate,
		TimeSpan           timeout,
		CancellationToken  cancellationToken)
	{
		var id  = Guid.NewGuid();
		var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

		if (_waiters.TryAdd(id, new(predicate, tcs)))
			Interlocked.Increment(ref _waiterCount);
		else
			throw new InvalidOperationException("Unable to register UCI line waiter.");

		if (timeout == Timeout.InfiniteTimeSpan && !cancellationToken.CanBeCanceled)
			try
			{
				return await tcs.Task.ConfigureAwait(false);
			}
			finally
			{
				if (_waiters.TryRemove(id, out _))
					Interlocked.Decrement(ref _waiterCount);
			}

		using var timeoutCts = timeout == Timeout.InfiniteTimeSpan ? null : new CancellationTokenSource(timeout);
		using var linked = timeoutCts is null
							   ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
							   : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		using var registration = linked.Token.Register(() => tcs.TrySetCanceled(linked.Token));

		try
		{
			return await tcs.Task.ConfigureAwait(false);
		}
		finally
		{
			if (_waiters.TryRemove(id, out _))
				Interlocked.Decrement(ref _waiterCount);
		}
	}

	/// <summary>
	///     Cancels and clears all registered waiters.
	/// </summary>
	public void CancelAll()
	{
		foreach (var (id, waiter) in _waiters)
		{
			if (!_waiters.TryRemove(id, out var removed)) continue;

			Interlocked.Decrement(ref _waiterCount);
			removed.CompletionSource.TrySetCanceled();
		}
	}

	/// <summary>
	///     Attempts to complete any registered waiters whose predicate matches the supplied line.
	/// </summary>
	public void Notify(string line)
	{
		if (!HasWaiters) return;

		foreach (var (id, waiter) in _waiters)
		{
			bool match;
			try
			{
				match = waiter.Predicate(line);
			}
			catch
			{
				match = false;
			}

			if (!match) continue;

			if (!_waiters.TryRemove(id, out var removed)) continue;

			Interlocked.Decrement(ref _waiterCount);
			removed.CompletionSource.TrySetResult(line);
		}
	}

	private readonly record struct WaiterRegistration(
		Func<string, bool>           Predicate,
		TaskCompletionSource<string> CompletionSource
	);
}
