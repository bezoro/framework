namespace Bezoro.Chess.UCI.Tests.TestHelpers;

/// <summary>
///     Helper methods for simplifying async test patterns and reducing boilerplate.
/// </summary>
public static class AsyncTestHelpers
{
	/// <summary>
	///     Executes an async operation with a timeout, throwing TimeoutException if it exceeds the timeout.
	/// </summary>
	/// <param name="operation">The async operation to execute.</param>
	/// <param name="timeout">Maximum time to wait for the operation.</param>
	/// <param name="ct">Cancellation token.</param>
	public static async Task WithTimeoutAsync(
		Func<CancellationToken, Task> operation,
		TimeSpan                      timeout = default,
		CancellationToken             ct      = default)
	{
		if (timeout == default)
			timeout = TestConstants.DefaultTimeout;

		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeoutCts.CancelAfter(timeout);

		try
		{
			await operation(timeoutCts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (OperationCanceledException)
		{
			throw new TimeoutException($"Operation did not complete within {timeout.TotalSeconds} seconds.");
		}
	}

	/// <summary>
	///     Waits for a condition to become true by polling at regular intervals.
	/// </summary>
	/// <param name="condition">Function that returns true when the condition is met.</param>
	/// <param name="pollInterval">Interval between condition checks.</param>
	/// <param name="timeout">Maximum time to wait for the condition.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>True if condition was met, false if timeout occurred.</returns>
	public static async Task<bool> WaitForConditionAsync(
		Func<bool>        condition,
		TimeSpan          pollInterval = default,
		TimeSpan          timeout      = default,
		CancellationToken ct           = default)
	{
		if (pollInterval == default)
			pollInterval = TestConstants.ShortDelay;

		if (timeout == default)
			timeout = TestConstants.DefaultTimeout;

		var startTime = DateTime.UtcNow;

		while (!ct.IsCancellationRequested)
		{
			if (condition())
				return true;

			if (DateTime.UtcNow - startTime >= timeout)
				return false;

			await Task.Delay(pollInterval, ct).ConfigureAwait(false);
		}

		return false;
	}

	/// <summary>
	///     Waits for an event to be raised by capturing it via a TaskCompletionSource.
	/// </summary>
	/// <typeparam name="T">The type of event data.</typeparam>
	/// <param name="subscribe">Action that subscribes to the event.</param>
	/// <param name="timeout">Maximum time to wait for the event.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The event data when raised.</returns>
	public static async Task<T> WaitForEventAsync<T>(
		Action<Action<T>> subscribe,
		TimeSpan          timeout = default,
		CancellationToken ct      = default)
	{
		if (timeout == default)
			timeout = TestConstants.DefaultTimeout;

		var        tcs     = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
		Action<T>? handler = null;

		handler = data => { tcs.TrySetResult(data); };

		subscribe(handler);

		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeoutCts.CancelAfter(timeout);

		try
		{
			return await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (OperationCanceledException)
		{
			throw new TimeoutException($"Event was not raised within {timeout.TotalSeconds} seconds.");
		}
	}

	/// <summary>
	///     Waits for multiple events to be raised, returning when any one is raised.
	/// </summary>
	/// <typeparam name="T">The type of event data.</typeparam>
	/// <param name="subscribe">Action that subscribes to the event.</param>
	/// <param name="predicate">Optional predicate to filter events.</param>
	/// <param name="timeout">Maximum time to wait for the event.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The event data when raised.</returns>
	public static async Task<T> WaitForEventAsync<T>(
		Action<Action<T>> subscribe,
		Func<T, bool>     predicate,
		TimeSpan          timeout = default,
		CancellationToken ct      = default)
	{
		if (timeout == default)
			timeout = TestConstants.DefaultTimeout;

		var        tcs     = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
		Action<T>? handler = null;

		handler = data =>
		{
			if (predicate == null || predicate(data))
				tcs.TrySetResult(data);
		};

		subscribe(handler);

		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeoutCts.CancelAfter(timeout);

		try
		{
			return await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (OperationCanceledException)
		{
			throw new TimeoutException(
				$"Event matching predicate was not raised within {timeout.TotalSeconds} seconds."
			);
		}
	}

	/// <summary>
	///     Executes an async operation with a timeout, throwing TimeoutException if it exceeds the timeout.
	/// </summary>
	/// <typeparam name="T">The return type of the operation.</typeparam>
	/// <param name="operation">The async operation to execute.</param>
	/// <param name="timeout">Maximum time to wait for the operation.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The result of the operation.</returns>
	public static async Task<T> WithTimeoutAsync<T>(
		Func<CancellationToken, Task<T>> operation,
		TimeSpan                         timeout = default,
		CancellationToken                ct      = default)
	{
		if (timeout == default)
			timeout = TestConstants.DefaultTimeout;

		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeoutCts.CancelAfter(timeout);

		try
		{
			return await operation(timeoutCts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (OperationCanceledException)
		{
			throw new TimeoutException($"Operation did not complete within {timeout.TotalSeconds} seconds.");
		}
	}

	/// <summary>
	///     Waits for an async enumerator to finish yielding items within the supplied timeout.
	/// </summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="enumerator">Enumerator to drain.</param>
	/// <param name="pendingMoveNext">Optional in-flight MoveNextAsync task started before the shutdown action.</param>
	/// <param name="timeout">Maximum time to wait for the enumerator to complete.</param>
	/// <param name="ct">Cancellation token.</param>
	public static async Task WaitForAsyncEnumeratorToCompleteAsync<T>(
		IAsyncEnumerator<T> enumerator,
		Task<bool>?         pendingMoveNext = null,
		TimeSpan            timeout         = default,
		CancellationToken   ct              = default)
	{
		if (timeout == default)
			timeout = TestConstants.DefaultTimeout;

		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeoutCts.CancelAfter(timeout);

		try
		{
			bool hasItem = pendingMoveNext is null
							   ? await enumerator.MoveNextAsync().AsTask().WaitAsync(timeoutCts.Token).ConfigureAwait(false)
							   : await pendingMoveNext.WaitAsync(timeoutCts.Token).ConfigureAwait(false);

			while (hasItem)
				hasItem = await enumerator.MoveNextAsync().AsTask().WaitAsync(timeoutCts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (OperationCanceledException)
		{
			throw new TimeoutException($"Enumerator did not complete within {timeout.TotalSeconds} seconds.");
		}
	}
}
