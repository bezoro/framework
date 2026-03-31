using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

namespace Bezoro.Chess.UCI.Protocol.API;

/// <summary>
///     Coordinates cancellable cached move analysis for a single analysis client across changing positions.
/// </summary>
public sealed class UciMoveAnalysisCoordinator(UciEngineClient client, int multiPvMoveTimeMs = 3_000, int fallbackMoveTimeMs = 250)
{
	private readonly object                  _sync = new();
	private          CancellationTokenSource? _cts;
	private          MoveAnalysisResult?      _cachedResult;
	private          string?                  _positionKey;
	private          Task<MoveAnalysisResult>? _runningTask;

	/// <summary>
	///     Returns the cached or running analysis for the supplied position key.
	/// </summary>
	/// <param name="positionKey">Position identifier used when starting the analysis.</param>
	/// <returns>Completed analysis, an empty result while cancelled, or the current running task result.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the coordinator is tracking a different position.</exception>
	public async Task<MoveAnalysisResult> GetAnalysisAsync(string positionKey)
	{
		Task<MoveAnalysisResult>? runningTask;
		MoveAnalysisResult?       cachedResult;

		lock (_sync)
		{
			if (!string.Equals(_positionKey, positionKey, StringComparison.Ordinal))
				throw new InvalidOperationException("Move analysis is not available for the current position.");

			cachedResult = _cachedResult;
			runningTask  = _runningTask;
		}

		if (cachedResult.HasValue)
			return cachedResult.Value;

		if (runningTask is null)
			return MoveAnalysisResult.Empty;

		return await runningTask.ConfigureAwait(false);
	}

	/// <summary>
	///     Cancels any in-flight analysis and clears cached state.
	/// </summary>
	public void Cancel()
	{
		CancellationTokenSource? ctsToCancel;
		lock (_sync)
		{
			ctsToCancel   = _cts;
			_cts          = null;
			_positionKey  = null;
			_runningTask  = null;
			_cachedResult = null;
		}

		if (ctsToCancel is null)
			return;

		try
		{
			ctsToCancel.Cancel();
		}
		finally
		{
			ctsToCancel.Dispose();
		}
	}

	/// <summary>
	///     Ensures analysis is running for the supplied position; repeated calls for the same key are ignored.
	/// </summary>
	/// <param name="positionKey">Stable identifier for the position, typically a FEN string.</param>
	/// <param name="moves">Move history used to set the engine position.</param>
	/// <param name="sideToMove">Side to move for the position: <c>w</c> or <c>b</c>.</param>
	/// <param name="playerColor">Player side: <c>w</c> or <c>b</c>.</param>
	/// <param name="legalMoves">Legal moves in lowercase UCI notation.</param>
	/// <param name="baselineCp">Optional centipawn baseline to subtract after perspective normalization.</param>
	/// <param name="currentScore">Current position score used to compute move deltas.</param>
	public void EnsureStarted(
		string                 positionKey,
		IReadOnlyList<string>  moves,
		char                   sideToMove,
		char                   playerColor,
		ImmutableArray<string> legalMoves,
		int                    baselineCp,
		PositionScore          currentScore)
	{
		CancellationTokenSource? ctsToCancel = null;

		lock (_sync)
		{
			if (string.Equals(_positionKey, positionKey, StringComparison.Ordinal) &&
				(_cachedResult.HasValue || _runningTask is { }))
			{
				return;
			}

			ctsToCancel   = _cts;
			_cts          = new();
			_positionKey  = positionKey;
			_cachedResult = null;

			ImmutableArray<string> movesCopy = [.. moves];
			var token = _cts.Token;
			_runningTask = AnalyzeAndCacheAsync(
				positionKey,
				movesCopy,
				sideToMove,
				playerColor,
				legalMoves,
				baselineCp,
				currentScore,
				token
			);
		}

		if (ctsToCancel is null)
			return;

		try
		{
			ctsToCancel.Cancel();
		}
		finally
		{
			ctsToCancel.Dispose();
		}
	}

	private async Task<MoveAnalysisResult> AnalyzeAndCacheAsync(
		string                 positionKey,
		ImmutableArray<string> moves,
		char                   sideToMove,
		char                   playerColor,
		ImmutableArray<string> legalMoves,
		int                    baselineCp,
		PositionScore          currentScore,
		CancellationToken      ct)
	{
		try
		{
			await client.SetPositionAsync(Fen.Default, moves, ct).ConfigureAwait(false);
			var evaluations = await client.AnalyzeLegalMovesAsync(
				sideToMove,
				playerColor,
				legalMoves,
				currentScore,
				baselineCp,
				multiPvMoveTimeMs,
				fallbackMoveTimeMs,
				ct
			).ConfigureAwait(false);

			var result = new MoveAnalysisResult(evaluations);
			lock (_sync)
			{
				if (string.Equals(_positionKey, positionKey, StringComparison.Ordinal) &&
					_cts is { IsCancellationRequested: false })
				{
					_cachedResult = result;
					_runningTask  = null;
				}
			}

			return result;
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			return MoveAnalysisResult.Empty;
		}
	}
}
