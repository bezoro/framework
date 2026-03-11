using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Bezoro.UCI.Domain.EngineClient;

/// <summary>
///     Maintains a registry of asynchronous waiters that complete when an engine output line satisfies a predicate.
///     Decouples imperative wait-points from the background read loop.
/// </summary>
internal sealed class UciLineWaiterRegistry
{
	private readonly LinkedList<WaiterRegistration> _waiters = [];
	private readonly object                         _waiterLock = new();
	private          int                            _waiterCount;

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
		var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		LinkedListNode<WaiterRegistration> node;

		lock (_waiterLock)
		{
			node = _waiters.AddLast(new WaiterRegistration(predicate, tcs));
			Interlocked.Increment(ref _waiterCount);
		}

		if (timeout == Timeout.InfiniteTimeSpan && !cancellationToken.CanBeCanceled)
			try
			{
				return await tcs.Task.ConfigureAwait(false);
			}
			finally
			{
				RemoveWaiter(node);
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
			RemoveWaiter(node);
		}
	}

	/// <summary>
	///     Cancels and clears all registered waiters.
	/// </summary>
	public void CancelAll()
	{
		WaiterRegistration[] waitersToCancel;
		lock (_waiterLock)
		{
			if (_waiters.Count == 0) return;

			waitersToCancel = [.. _waiters];
			_waiters.Clear();
			Volatile.Write(ref _waiterCount, 0);
		}

		foreach (WaiterRegistration waiter in waitersToCancel)
			waiter.CompletionSource.TrySetCanceled();
	}

	/// <summary>
	///     Attempts to complete the earliest registered waiter whose predicate matches the supplied line.
	/// </summary>
	public void Notify(string line)
	{
		if (!HasWaiters) return;

		TaskCompletionSource<string>? completion = null;

		lock (_waiterLock)
		{
			var node = _waiters.First;
			while (node is { })
			{
				var current = node;
				node = node.Next;

				bool match;
				try
				{
					match = current.Value.Predicate(line);
				}
				catch
				{
					match = false;
				}

				if (!match) continue;

				_waiters.Remove(current);
				Interlocked.Decrement(ref _waiterCount);
				completion = current.Value.CompletionSource;
				break;
			}
		}

		completion?.TrySetResult(line);
	}

	private void RemoveWaiter(LinkedListNode<WaiterRegistration> node)
	{
		lock (_waiterLock)
		{
			if (node.List is null) return;

			_waiters.Remove(node);
			Interlocked.Decrement(ref _waiterCount);
		}
	}

	private readonly record struct WaiterRegistration(
		Func<string, bool>           Predicate,
		TaskCompletionSource<string> CompletionSource
	);
}
