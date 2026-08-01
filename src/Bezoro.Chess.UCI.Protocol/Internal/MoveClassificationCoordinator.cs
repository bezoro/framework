using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

namespace Bezoro.Chess.UCI.Protocol.Internal;

internal sealed class MoveClassificationCoordinator : IDisposable
{
	private readonly Func<Fen, string, MoveClassification> _classifyMoveFully;
	private readonly Action?                               _beforeWorkerRetires;
	private readonly Dictionary<string, ImmutableDictionary<string, MoveClassification>> _classificationsByPosition =
		new(StringComparer.Ordinal);
	private readonly Dictionary<string, TaskCompletionSource<ImmutableDictionary<string, MoveClassification>>> _waiters =
		new(StringComparer.Ordinal);
	private readonly HashSet<string>                      _queuedPositionKeys = new(StringComparer.Ordinal);
	private readonly object                               _sync               = new();
	private readonly Queue<PendingClassificationPosition> _pendingPositions   = new();
	private          WorkerGeneration                     _generation         = new();

	public MoveClassificationCoordinator()
		: this(static (fen, move) => fen.ClassifyMoveFully(move)) { }

	internal MoveClassificationCoordinator(
		Func<Fen, string, MoveClassification> classifyMoveFully,
		Action?                               beforeWorkerRetires = null)
	{
		_classifyMoveFully   = classifyMoveFully;
		_beforeWorkerRetires = beforeWorkerRetires;
	}

	internal Task? ActiveWorker
	{
		get
		{
			lock (_sync)
				return _generation.WorkerTask;
		}
	}

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
		WorkerGeneration retiredGeneration;

		lock (_sync)
		{
			retiredGeneration = _generation;
			_generation       = new();
			waiters = [.. _waiters.Values];

			foreach (var positionKey in _classificationsByPosition.Keys.ToArray())
			{
				if (!retainedPositionKeys.Contains(positionKey))
					_classificationsByPosition.Remove(positionKey);
			}

			_waiters.Clear();
			_pendingPositions.Clear();
			_queuedPositionKeys.Clear();
		}

		try
		{
			retiredGeneration.CancellationSource.Cancel();
		}
		finally
		{
			retiredGeneration.CancellationSource.Dispose();
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
		var generation = _generation;
		if (generation.WorkerTask is { IsCompleted: false })
			return;

		var workerIdentity = new object();
		generation.WorkerIdentity = workerIdentity;
		generation.WorkerTask     = Task.Run(() => RunWorker(generation, workerIdentity), generation.Token);
	}

	private TaskCompletionSource<ImmutableDictionary<string, MoveClassification>> GetOrCreateWaiter(string positionKey)
	{
		if (_waiters.TryGetValue(positionKey, out var waiter))
			return waiter;

		waiter               = new(TaskCreationOptions.RunContinuationsAsynchronously);
		_waiters[positionKey] = waiter;
		return waiter;
	}

	private void RunWorker(WorkerGeneration generation, object workerIdentity)
	{
		try
		{
			while (true)
			{
				PendingClassificationPosition position = default;
				bool shouldRetire;
				lock (_sync)
				{
					if (!ReferenceEquals(_generation, generation))
						return;

					shouldRetire = _pendingPositions.Count == 0;
					if (shouldRetire)
					{
						if (ReferenceEquals(generation.WorkerIdentity, workerIdentity))
						{
							generation.WorkerIdentity = null;
							generation.WorkerTask     = null;
						}
					}
					else
						position = _pendingPositions.Dequeue();
				}

				if (shouldRetire)
				{
					_beforeWorkerRetires?.Invoke();
					return;
				}

				try
				{
					var completed = Classify(position, generation);
					lock (_sync)
					{
						if (!ReferenceEquals(_generation, generation))
							return;

						_queuedPositionKeys.Remove(position.PositionKey);
						GetOrCreateWaiter(position.PositionKey).TrySetResult(completed);
					}
				}
				catch (OperationCanceledException) when (generation.Token.IsCancellationRequested)
				{
					return;
				}
				catch (Exception ex)
				{
					lock (_sync)
					{
						if (!ReferenceEquals(_generation, generation))
							return;

						_queuedPositionKeys.Remove(position.PositionKey);
						GetOrCreateWaiter(position.PositionKey).TrySetException(ex);
					}

					throw;
				}
			}
		}
		finally
		{
			lock (_sync)
			{
				if (ReferenceEquals(_generation, generation) &&
					ReferenceEquals(generation.WorkerIdentity, workerIdentity))
				{
					generation.WorkerIdentity = null;
					generation.WorkerTask     = null;
					if (_pendingPositions.Count > 0)
						EnsureWorkerStarted();
				}
			}
		}
	}

	private ImmutableDictionary<string, MoveClassification> Classify(
		PendingClassificationPosition position,
		WorkerGeneration              generation)
	{
		foreach (string move in position.LegalMoves)
		{
			generation.Token.ThrowIfCancellationRequested();

			MoveClassification current = default;
			var hasResolved = false;
			lock (_sync)
			{
				ThrowIfRetired(generation);
				hasResolved = _classificationsByPosition.TryGetValue(position.PositionKey, out var known) &&
							  known.TryGetValue(move, out current) &&
							  current.IsResolved;
			}

			var resolved = hasResolved
							   ? current
							   : _classifyMoveFully(position.Fen, move);
			generation.Token.ThrowIfCancellationRequested();

			lock (_sync)
			{
				ThrowIfRetired(generation);
				_classificationsByPosition[position.PositionKey] =
					GetKnown(position.PositionKey).SetItem(move, resolved);
			}
		}

		lock (_sync)
		{
			ThrowIfRetired(generation);
			return GetKnown(position.PositionKey);
		}
	}

	private void ThrowIfRetired(WorkerGeneration generation)
	{
		if (!ReferenceEquals(_generation, generation))
			throw new OperationCanceledException(generation.Token);
	}

	private sealed class WorkerGeneration
	{
		public WorkerGeneration()
		{
			CancellationSource = new();
			Token              = CancellationSource.Token;
		}

		public CancellationTokenSource CancellationSource { get; }
		public CancellationToken       Token              { get; }
		public object?                 WorkerIdentity     { get; set; }
		public Task?                   WorkerTask         { get; set; }
	}

	private readonly record struct PendingClassificationPosition(
		string                 PositionKey,
		Fen                    Fen,
		ImmutableArray<string> LegalMoves
	);
}
