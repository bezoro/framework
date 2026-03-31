using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

namespace Bezoro.Chess.UCI.Protocol.API;

/// <summary>
///     Coordinates ordered full-strength position analysis over a dedicated client.
///     Positions are analyzed in FIFO order, and completed results are cached by position key for later reuse.
/// </summary>
public sealed class UciPositionAnalysisCoordinator : IDisposable
{
	private readonly UciEngineClient _client;
	private readonly int             _multiPvMoveTimeMs;
	private readonly int             _fallbackMoveTimeMs;
	private readonly object _sync = new();
	private readonly Queue<PositionAnalysisRequest> _pendingRequests = new();
	private readonly HashSet<string> _trackedPositionKeys = new(StringComparer.Ordinal);
	private readonly Dictionary<string, PositionAnalysisResult> _completedAnalyses = new(StringComparer.Ordinal);
	private readonly Dictionary<string, TaskCompletionSource<PositionAnalysisResult>> _waiters =
		new(StringComparer.Ordinal);
	private CancellationTokenSource _lifetimeCts = new();
	private string? _currentProcessingPositionKey;
	private Task? _workerTask;

	/// <summary>
	///     Initializes a new coordinator over a dedicated analysis client.
	/// </summary>
	public UciPositionAnalysisCoordinator(
		UciEngineClient client,
		int             multiPvMoveTimeMs  = 3_000,
		int             fallbackMoveTimeMs = 250)
	{
		_client             = client ?? throw new ArgumentNullException(nameof(client));
		_multiPvMoveTimeMs  = multiPvMoveTimeMs;
		_fallbackMoveTimeMs = fallbackMoveTimeMs;
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
			if (_completedAnalyses.ContainsKey(positionKey) || !_trackedPositionKeys.Add(positionKey))
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

			if (!_waiters.ContainsKey(positionKey))
				_waiters[positionKey] = new(TaskCreationOptions.RunContinuationsAsynchronously);

			if (_workerTask is null || _workerTask.IsCompleted)
				_workerTask = Task.Run(ProcessLoopAsync);
		}
	}

	/// <summary>
	///     Compatibility alias for <see cref="Enqueue" />.
	/// </summary>
	public void EnqueueLatest(
		string                 positionKey,
		IReadOnlyList<string>  moves,
		char                   sideToMove,
		char                   playerColor,
		ImmutableArray<string> legalMoves) =>
		Enqueue(positionKey, moves, sideToMove, playerColor, legalMoves);

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
	///     Compatibility alias for <see cref="TryGetAnalysis" />.
	/// </summary>
	public bool TryGetLatestAnalysis(string positionKey, out PositionAnalysisResult analysis) =>
		TryGetAnalysis(positionKey, out analysis);

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
	public void Cancel()
	{
		CancellationTokenSource lifetimeCts;
		TaskCompletionSource<PositionAnalysisResult>[] waiters;

		lock (_sync)
		{
			lifetimeCts                 = _lifetimeCts;
			_lifetimeCts                = new();
			waiters                     = [.. _waiters.Values];
			_pendingRequests.Clear();
			_waiters.Clear();
			_trackedPositionKeys.Clear();
			_completedAnalyses.Clear();
			_currentProcessingPositionKey = null;
			_workerTask                 = null;
		}

		try
		{
			lifetimeCts.Cancel();
		}
		finally
		{
			lifetimeCts.Dispose();
		}

		foreach (var waiter in waiters)
			waiter.TrySetCanceled();
	}

	private async Task ProcessLoopAsync()
	{
		while (true)
		{
			PositionAnalysisRequest request;
			CancellationToken token;

			lock (_sync)
			{
				if (_lifetimeCts.IsCancellationRequested)
				{
					_workerTask = null;
					return;
				}

				if (!_pendingRequests.TryDequeue(out request))
				{
					_workerTask = null;
					return;
				}

				_currentProcessingPositionKey = request.PositionKey;
				token                         = _lifetimeCts.Token;
			}

			try
			{
				await _client.SetPositionAsync(Fen.Default, request.Moves, token).ConfigureAwait(false);
				var analysis = await _client.AnalyzePositionAsync(
					request.SideToMove,
					request.PlayerColor,
					request.LegalMoves,
					_multiPvMoveTimeMs,
					_fallbackMoveTimeMs,
					token
				).ConfigureAwait(false);

				TaskCompletionSource<PositionAnalysisResult>? waiter = null;
				lock (_sync)
				{
					_completedAnalyses[request.PositionKey] = analysis;
					_trackedPositionKeys.Remove(request.PositionKey);
					_currentProcessingPositionKey = null;
					if (_waiters.TryGetValue(request.PositionKey, out waiter))
						_waiters.Remove(request.PositionKey);
				}

				waiter?.TrySetResult(analysis);
			}
			catch (OperationCanceledException) when (token.IsCancellationRequested)
			{
				lock (_sync)
				{
					_currentProcessingPositionKey = null;
					_workerTask                   = null;
				}

				return;
			}
			catch (Exception ex)
			{
				TaskCompletionSource<PositionAnalysisResult>? waiter = null;
				lock (_sync)
				{
					_trackedPositionKeys.Remove(request.PositionKey);
					_currentProcessingPositionKey = null;
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

	private readonly record struct PositionAnalysisRequest(
		string                 PositionKey,
		ImmutableArray<string> Moves,
		char                   SideToMove,
		char                   PlayerColor,
		ImmutableArray<string> LegalMoves
	);
}
