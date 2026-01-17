using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Bezoro.UCI.Domain.Common.Helpers;

/// <summary>
/// Manages gates for coordinating start and stop operations.
/// </summary>
internal sealed class GateManager
{
	private int                                  _startGate;
	private int                                  _stopGate;
	private TaskCompletionSource<object?>?       _startingTcs;
	private TaskCompletionSource<object?>?       _stoppingTcs;

	/// <summary>
	/// Acquires the start gate, waiting for any existing start operation to complete.
	/// </summary>
	public async Task<bool> AcquireStartGateAsync(CancellationToken token)
	{
		if (Interlocked.CompareExchange(ref _startGate, 1, 0) == 0)
			return true;

		while (true)
		{
			var published = Volatile.Read(ref _startingTcs);
			if (published is { })
			{
				await AwaitWithCancellation(published.Task, token).ConfigureAwait(false);
				return false;
			}

			if (Volatile.Read(ref _startGate) == 0 && Interlocked.CompareExchange(ref _startGate, 1, 0) == 0)
				return true;

			token.ThrowIfCancellationRequested();
			await Task.Yield();
		}
	}

	/// <summary>
	/// Releases the start gate.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void ReleaseStartGate()
	{
		Interlocked.Exchange(ref _startGate, 0);
		Volatile.Write(ref _startingTcs, null);
	}

	/// <summary>
	/// Publishes a starting signal.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public TaskCompletionSource<object?> PublishStartingSignal()
	{
		var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
		Volatile.Write(ref _startingTcs, tcs);
		return tcs;
	}

	/// <summary>
	/// Gets the current starting signal, if any.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public TaskCompletionSource<object?>? GetStartingSignal() => Volatile.Read(ref _startingTcs);

	/// <summary>
	/// Awaits an existing start operation if any.
	/// </summary>
	public static Task AwaitExistingStartAsync(TaskCompletionSource<object?>? tcs, CancellationToken token) =>
		tcs is null ? Task.CompletedTask : AwaitWithCancellation(tcs.Task, token);

	/// <summary>
	/// Awaits an operation with cancellation support.
	/// </summary>
	private static async Task AwaitWithCancellation(Task task, CancellationToken ct)
	{
		if (!ct.CanBeCanceled)
		{
			await task.ConfigureAwait(false);
			return;
		}

		if (task.IsCompleted)
		{
			await task.ConfigureAwait(false);
			return;
		}

		ct.ThrowIfCancellationRequested();

		var       tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var reg = ct.Register(static s => ((TaskCompletionSource<object?>)s!).TrySetResult(null), tcs);

		var completed = await Task.WhenAny(task, tcs.Task).ConfigureAwait(false);
		if (completed == tcs.Task)
			throw new OperationCanceledException(ct);

		await task.ConfigureAwait(false);
	}

	/// <summary>
	/// Awaits an existing stop operation if any, or acquires the stop gate.
	/// </summary>
	public async Task<bool> AwaitExistingStopIfAnyAsync(CancellationToken ct)
	{
		var existingStop = _stoppingTcs;
		if (existingStop != null)
		{
			await AwaitWithCancellation(existingStop.Task, ct).ConfigureAwait(false);
			return true;
		}

		if (Interlocked.CompareExchange(ref _stopGate, 1, 0) != 0)
			while (true)
			{
				existingStop = Volatile.Read(ref _stoppingTcs);
				if (existingStop != null)
				{
					await AwaitWithCancellation(existingStop.Task, ct).ConfigureAwait(false);
					return true;
				}

				if (Volatile.Read(ref _stopGate) == 0 && Interlocked.CompareExchange(ref _stopGate, 1, 0) == 0)
					break;

				ct.ThrowIfCancellationRequested();
				await Task.Yield();
			}

		return false;
	}

	/// <summary>
	/// Releases the stop gate.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void ReleaseStopGate()
	{
		Interlocked.Exchange(ref _stopGate, 0);
		Volatile.Write(ref _stoppingTcs, null);
	}

	/// <summary>
	/// Publishes a stopping signal.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public TaskCompletionSource<object?> PublishStoppingSignal()
	{
		var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
		Volatile.Write(ref _stoppingTcs, tcs);
		return tcs;
	}
}

