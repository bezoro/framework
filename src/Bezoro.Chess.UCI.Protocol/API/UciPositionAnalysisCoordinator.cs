using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using Bezoro.Chess.UCI.Protocol.Internal;

namespace Bezoro.Chess.UCI.Protocol.API;

/// <summary>
///     Coordinates ordered full-strength position analysis over a dedicated client.
///     Positions are analyzed in FIFO order, and completed results are cached by position key for later reuse.
/// </summary>
public sealed class UciPositionAnalysisCoordinator : IDisposable
{
	private readonly Func<PositionAnalysisWorkItem, CancellationToken, Task<PositionAnalysisResult>> _analyzePositionAsync;
	private readonly Action? _beforeWorkerRetires;
	private readonly Action? _afterWorkDequeued;
	private readonly object _sync = new();
	private readonly Queue<PositionAnalysisWorkItem> _pendingRequests = new();
	private readonly Dictionary<string, PositionAnalysisResult> _completedAnalyses = new(StringComparer.Ordinal);
	private readonly Dictionary<string, TaskCompletionSource<PositionAnalysisResult>> _waiters =
		new(StringComparer.Ordinal);
	private CancellationTokenSource _lifetimeCts = new();
	private Task? _workerTask;

	internal Task? ActiveWorker
	{
		get
		{
			lock (_sync)
			{
				return _workerTask;
			}
		}
	}

	/// <summary>
	///     Initializes a new coordinator over a dedicated analysis client.
	/// </summary>
	public UciPositionAnalysisCoordinator(
		UciEngineClient client,
		int             multiPvMoveTimeMs  = 3_000,
		int             fallbackMoveTimeMs = 250)
		: this(CreateAnalyzer(client, multiPvMoveTimeMs, fallbackMoveTimeMs)) { }

	internal UciPositionAnalysisCoordinator(
		Func<PositionAnalysisWorkItem, CancellationToken, Task<PositionAnalysisResult>> analyzePositionAsync,
		Action? beforeWorkerRetires = null,
		Action? afterWorkDequeued = null)
	{
		_analyzePositionAsync = analyzePositionAsync ?? throw new ArgumentNullException(nameof(analyzePositionAsync));
		_beforeWorkerRetires = beforeWorkerRetires;
		_afterWorkDequeued = afterWorkDequeued;
	}

	/// <summary>
	///     Queues the supplied position for ordered analysis when it has not already been completed or queued.
	/// </summary>
	public void Enqueue(
		string                 positionKey,
		IReadOnlyList<string>  moves,
		char                   sideToMove,
		char                   playerColor,
		ImmutableArray<string> legalMoves)
	{
		lock (_sync)
		{
			if (_completedAnalyses.ContainsKey(positionKey) || _waiters.ContainsKey(positionKey))
				return;

			_pendingRequests.Enqueue(
				new(
					positionKey,
					[.. moves],
					sideToMove,
					playerColor,
					legalMoves
				)
			);

			_waiters[positionKey] = new(TaskCreationOptions.RunContinuationsAsynchronously);

			if (_workerTask is null || _workerTask.IsCompleted)
				_workerTask = Task.Run(ProcessLoopAsync);
		}
	}

	/// <summary>
	///     Attempts to read a completed analysis for the supplied position.
	/// </summary>
	public bool TryGetAnalysis(string positionKey, out PositionAnalysisResult analysis)
	{
		lock (_sync)
		{
			if (_completedAnalyses.TryGetValue(positionKey, out analysis))
				return true;
		}

		analysis = default;
		return false;
	}

	/// <summary>
	///     Awaits completion of the supplied position when it has been queued for analysis.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when the coordinator is not tracking the supplied position.</exception>
	public Task<PositionAnalysisResult> GetAnalysisAsync(string positionKey)
	{
		lock (_sync)
		{
			if (_completedAnalyses.TryGetValue(positionKey, out var analysis))
				return Task.FromResult(analysis);

			if (_waiters.TryGetValue(positionKey, out var waiter))
				return waiter.Task;
		}

		throw new InvalidOperationException("Position analysis is not available for the supplied position.");
	}

	/// <summary>
	///     Cancels in-flight work and clears pending and completed state.
	/// </summary>
	public void Cancel() => CancelCore(null);

	internal void CancelPendingAndRetainCompleted(ISet<string> retainedPositionKeys)
	{
		if (retainedPositionKeys is null)
			throw new ArgumentNullException(nameof(retainedPositionKeys));

		CancelCore(retainedPositionKeys);
	}

	private async Task ProcessLoopAsync()
	{
		while (true)
		{
			PositionAnalysisWorkItem request;
			CancellationTokenSource? generation;
			CancellationToken token;

			lock (_sync)
			{
				if (_pendingRequests.TryDequeue(out request))
				{
					generation = _lifetimeCts;
					token = generation.Token;
				}
				else
				{
					_workerTask = null;
					generation = null;
					token = default;
				}
			}

			if (generation is null)
			{
				_beforeWorkerRetires?.Invoke();
				return;
			}

			_afterWorkDequeued?.Invoke();

			try
			{
				var analysis = await _analyzePositionAsync(request, token).ConfigureAwait(false);

				TaskCompletionSource<PositionAnalysisResult>? waiter = null;
				lock (_sync)
				{
					if (!ReferenceEquals(generation, _lifetimeCts))
						continue;

					_completedAnalyses[request.PositionKey] = analysis;
					if (_waiters.TryGetValue(request.PositionKey, out waiter))
						_waiters.Remove(request.PositionKey);
				}

				waiter?.TrySetResult(analysis);
			}
			catch (OperationCanceledException) when (token.IsCancellationRequested)
			{
				continue;
			}
			catch (Exception ex)
			{
				TaskCompletionSource<PositionAnalysisResult>? waiter = null;
				lock (_sync)
				{
					if (!ReferenceEquals(generation, _lifetimeCts))
						continue;

					if (_waiters.TryGetValue(request.PositionKey, out waiter))
						_waiters.Remove(request.PositionKey);
				}

				waiter?.TrySetException(ex);
			}
		}
	}

	/// <summary>
	///     Cancels queued work and releases coordinator resources.
	/// </summary>
	public void Dispose() => Cancel();

	private void CancelCore(ISet<string>? retainedPositionKeys)
	{
		CancellationTokenSource retiredGeneration;
		TaskCompletionSource<PositionAnalysisResult>[] waiters;

		lock (_sync)
		{
			retiredGeneration = _lifetimeCts;
			_lifetimeCts = new();
			waiters = [.. _waiters.Values];
			_pendingRequests.Clear();
			_waiters.Clear();

			if (retainedPositionKeys is null)
			{
				_completedAnalyses.Clear();
			}
			else
			{
				foreach (var positionKey in _completedAnalyses.Keys.ToArray())
				{
					if (!retainedPositionKeys.Contains(positionKey))
						_completedAnalyses.Remove(positionKey);
				}
			}
		}

		try
		{
			retiredGeneration.Cancel();
		}
		finally
		{
			retiredGeneration.Dispose();
		}

		foreach (var waiter in waiters)
			waiter.TrySetCanceled();
	}

	private static Func<PositionAnalysisWorkItem, CancellationToken, Task<PositionAnalysisResult>> CreateAnalyzer(
		UciEngineClient client,
		int multiPvMoveTimeMs,
		int fallbackMoveTimeMs)
	{
		if (client is null) throw new ArgumentNullException(nameof(client));

		return async (workItem, token) =>
		{
			await client.SetPositionAsync(Fen.Default, workItem.Moves, token).ConfigureAwait(false);
			return await client.AnalyzePositionAsync(
				workItem.SideToMove,
				workItem.PlayerColor,
				workItem.LegalMoves,
				multiPvMoveTimeMs,
				fallbackMoveTimeMs,
				token
			).ConfigureAwait(false);
		};
	}
}
