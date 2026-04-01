using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

namespace Bezoro.Chess.UCI.Protocol.Internal;

internal sealed class MoveClassificationCoordinator : IDisposable
{
	private readonly Dictionary<string, ImmutableDictionary<string, MoveClassification>> _classificationsByPosition =
		new(StringComparer.Ordinal);
	private readonly Dictionary<string, TaskCompletionSource<ImmutableDictionary<string, MoveClassification>>> _waiters =
		new(StringComparer.Ordinal);
	private readonly HashSet<string>                      _queuedPositionKeys = new(StringComparer.Ordinal);
	private readonly object                               _sync               = new();
	private readonly Queue<PendingClassificationPosition> _pendingPositions   = new();
	private          CancellationTokenSource              _lifetimeCts        = new();
	private          Task?                                _workerTask;

	public ImmutableDictionary<string, MoveClassification> GetKnown(string positionKey)
	{
		lock (_sync)
		{
			return _classificationsByPosition.TryGetValue(positionKey, out var classifications)
					   ? classifications
					   : ImmutableDictionary<string, MoveClassification>.Empty.WithComparers(StringComparer.Ordinal);
		}
	}

	public void Enqueue(string positionKey, Fen fen, ImmutableArray<string> legalMoves)
	{
		var structural = fen.ClassifyMoves(legalMoves);

		lock (_sync)
		{
			if (_classificationsByPosition.TryGetValue(positionKey, out var existing))
				structural = MergeClassifications(structural, existing);

			_classificationsByPosition[positionKey] = structural;

			var waiter = GetOrCreateWaiter(positionKey);
			if (legalMoves.IsDefaultOrEmpty ||
				structural.Values.All(static classification => classification.IsResolved))
			{
				waiter.TrySetResult(structural);
				return;
			}

			if (!_queuedPositionKeys.Add(positionKey))
				return;

			_pendingPositions.Enqueue(new(positionKey, fen, legalMoves));
			EnsureWorkerStarted();
		}
	}

	public Task<ImmutableDictionary<string, MoveClassification>> WaitAsync(
		string            positionKey,
		CancellationToken ct = default)
	{
		Task<ImmutableDictionary<string, MoveClassification>>? waiterTask;

		lock (_sync)
		{
			waiterTask = _waiters.TryGetValue(positionKey, out var waiter)
							 ? waiter.Task
							 : null;
		}

		return waiterTask is null
				   ? Task.FromResult(GetKnown(positionKey))
				   : WaitWithCancellationAsync(waiterTask, ct);
	}

	public void Cancel() => CancelPendingAndRetain(new HashSet<string>(StringComparer.Ordinal));

	public void CancelPendingAndRetain(ISet<string> retainedPositionKeys)
	{
		if (retainedPositionKeys is null)
			throw new ArgumentNullException(nameof(retainedPositionKeys));

		TaskCompletionSource<ImmutableDictionary<string, MoveClassification>>[] waiters;
		var lifetimeCts = Interlocked.Exchange(ref _lifetimeCts, new());

		try
		{
			lifetimeCts.Cancel();
		}
		finally
		{
			lifetimeCts.Dispose();
		}

		lock (_sync)
		{
			waiters = [.. _waiters.Values];

			foreach (var positionKey in _classificationsByPosition.Keys.ToArray())
			{
				if (!retainedPositionKeys.Contains(positionKey))
					_classificationsByPosition.Remove(positionKey);
			}

			_waiters.Clear();
			_pendingPositions.Clear();
			_queuedPositionKeys.Clear();
			_workerTask = null;
		}

		foreach (var waiter in waiters)
			waiter.TrySetCanceled();
	}

	public void Dispose() => Cancel();

	private static ImmutableDictionary<string, MoveClassification> MergeClassifications(
		ImmutableDictionary<string, MoveClassification> structural,
		ImmutableDictionary<string, MoveClassification> existing)
	{
		if (existing.Count == 0)
			return structural;

		var builder = structural.ToBuilder();
		foreach ((string move, var classification) in existing)
		{
			if (!classification.IsResolved)
				continue;

			if (builder.ContainsKey(move))
				builder[move] = classification;
		}

		return builder.ToImmutable();
	}

	private static async Task<T> WaitWithCancellationAsync<T>(Task<T> task, CancellationToken ct)
	{
#if NET9_0
		return await task.WaitAsync(ct).ConfigureAwait(false);
#else
		if (!ct.CanBeCanceled)
			return await task.ConfigureAwait(false);

		var cancellationTask = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var registration = ct.Register(
			static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
			cancellationTask
		);

		if (task != await Task.WhenAny(task, cancellationTask.Task).ConfigureAwait(false))
			throw new OperationCanceledException(ct);

		return await task.ConfigureAwait(false);
#endif
	}

	private void EnsureWorkerStarted()
	{
		if (_workerTask is { IsCompleted: false })
			return;

		_workerTask = RunWorkerAsync(_lifetimeCts.Token);
	}

	private TaskCompletionSource<ImmutableDictionary<string, MoveClassification>> GetOrCreateWaiter(string positionKey)
	{
		if (_waiters.TryGetValue(positionKey, out var waiter))
			return waiter;

		waiter               = new(TaskCreationOptions.RunContinuationsAsynchronously);
		_waiters[positionKey] = waiter;
		return waiter;
	}

	private async Task RunWorkerAsync(CancellationToken ct)
	{
		while (true)
		{
			PendingClassificationPosition position;
			lock (_sync)
			{
				if (_pendingPositions.Count == 0)
				{
					_workerTask = null;
					return;
				}

				position = _pendingPositions.Dequeue();
			}

			try
			{
				var completed = await ClassifyAsync(position, ct).ConfigureAwait(false);
				lock (_sync)
				{
					_queuedPositionKeys.Remove(position.PositionKey);
					GetOrCreateWaiter(position.PositionKey).TrySetResult(completed);
				}
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				return;
			}
			catch (Exception ex)
			{
				lock (_sync)
				{
					_queuedPositionKeys.Remove(position.PositionKey);
					GetOrCreateWaiter(position.PositionKey).TrySetException(ex);
				}

				throw;
			}
		}
	}

	private Task<ImmutableDictionary<string, MoveClassification>> ClassifyAsync(
		PendingClassificationPosition position,
		CancellationToken             ct)
	{
		foreach (string move in position.LegalMoves)
		{
			ct.ThrowIfCancellationRequested();

			var resolved = GetKnown(position.PositionKey).TryGetValue(move, out var current) && current.IsResolved
							   ? current
							   : position.Fen.ClassifyMoveFully(move);

			lock (_sync)
			{
				_classificationsByPosition[position.PositionKey] =
					GetKnown(position.PositionKey).SetItem(move, resolved);
			}
		}

		return Task.FromResult(GetKnown(position.PositionKey));
	}

	private readonly record struct PendingClassificationPosition(
		string                 PositionKey,
		Fen                    Fen,
		ImmutableArray<string> LegalMoves
	);
}
