using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;

namespace Bezoro.UCI.Domain;

internal sealed class MoveClassificationEngine(
	string               enginePath,
	IEnumerable<string>? args             = null,
	string?              workingDirectory = null
) : IAsyncDisposable
{
	private readonly
		ConcurrentDictionary<(string Fen, string Move, uint Depth), (MoveScore Score, bool IsMate, bool IsStalemate)>
		_moveEvalCache = new();
	private readonly ConcurrentDictionary<(string Fen, string Move), bool> _isMateCache      = new();
	private readonly ConcurrentDictionary<(string Fen, string Move), bool> _isStalemateCache = new();
	private readonly IEnumerable<string>?                                  _args             = args;

	private readonly QuickInfoEngine _quick = new(enginePath, args, workingDirectory);

	private readonly string _enginePath = enginePath ?? throw new ArgumentNullException(nameof(enginePath));
	private          bool   _started;

	private ProcessUciTransport? _transport;
	private UciEngineClient?     _client;

	public async IAsyncEnumerable<Move> ClassifyAsync(
		Fen                                        fen,
		uint                                       perMoveDepth = 6,
		[EnumeratorCancellation] CancellationToken ct           = default)
	{
		EnsureStarted();

		// Fetch legal moves using the quick engine
		await _quick.SetPositionAsync(fen, null, ct).ConfigureAwait(false);
		var legalMoves = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);
		if (legalMoves is null || legalMoves.Count == 0)
			yield break;

		foreach (string? move in legalMoves)
		{
			if (string.IsNullOrWhiteSpace(move)) continue;

			var resultTuple = await EvaluateMoveAtRootAsync(fen, move, perMoveDepth, ct).ConfigureAwait(false);
			if (resultTuple.HasValue)
				yield return resultTuple.Value;
		}
	}

	public async Task NewGameAsync(CancellationToken ct = default)
	{
		await _client.UciNewGameAsync(ct);
	}

	public async Task StartAsync(CancellationToken ct = default)
	{
		await _quick.StartAsync(ct).ConfigureAwait(false);

		_transport = new(_enginePath, _args, workingDirectory);
		_client    = new(_transport);
		await _client.StartAsync(ct).ConfigureAwait(false);
		await _client.SetOptionAsync("Threads", "2", ct).ConfigureAwait(false);
		await _client.SetOptionAsync("MultiPv", "1", ct).ConfigureAwait(false);

		ClearCaches();

		_started = true;
	}

	public async Task StopAsync(CancellationToken ct = default)
	{
		await _quick.StopAsync(ct).ConfigureAwait(false);

		// Clear caches when engine session ends
		ClearCaches();

		if (_client is { })
		{
			try
			{
				await _client.StopAsync(ct).ConfigureAwait(false);
			}
			catch
			{
				/* best-effort */
			}
		}

		_started = false;
	}

	public async Task<bool> IsCheckmateAsync(
		Fen               fen,
		string            move,
		CancellationToken ct = default)
	{
		EnsureStarted();

		var key = (fen.ToString(), move);
		if (_isMateCache.TryGetValue(key, out bool cached))
			return cached;

		await _client.SetPositionAsync(fen, null, ct).ConfigureAwait(false);
		var  result = await _client.GoAsync(new() { Depth = 1, SearchMoves = [move] }, ct).ConfigureAwait(false);
		bool isMate = !result.BestCpScore.HasValue && result.MateScore == 1;

		_isMateCache[key] = isMate;
		return isMate;
	}

	public async Task<bool> IsStalemateAsync(
		Fen               fen,
		string            move,
		CancellationToken ct = default)
	{
		EnsureStarted();

		var key = (fen.ToString(), move);
		if (_isStalemateCache.TryGetValue(key, out bool cached))
			return cached;

		// Play the move and see if opponent has any legal moves
		await _quick.SetPositionAsync(fen, [move], ct).ConfigureAwait(false);
		var replies = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);
		if (replies is { Count: > 0 })
		{
			_isStalemateCache[key] = false;
			return false;
		}

		// No legal replies: differentiate stalemate from checkmate using the main engine
		bool isMate      = await IsCheckmateAsync(fen, move, ct).ConfigureAwait(false);
		bool isStalemate = !isMate;

		_isStalemateCache[key] = isStalemate;
		return isStalemate;
	}

	public async Task<Move?> ClassifyMoveAsync(
		Fen               fen,
		BoardState        board,
		string            move,
		uint              perMoveDepth = 6,
		CancellationToken ct           = default)
	{
		EnsureStarted();
		// Validate that the move is legal in the given position
		await _quick.SetPositionAsync(fen, null, ct).ConfigureAwait(false);
		var legalMoves = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);

		if (legalMoves is null || !legalMoves.Contains(move))
			throw new ArgumentException($"The move {move} is not legal in position {fen}");

		return await EvaluateMoveAtRootAsync(fen, move, perMoveDepth, ct).ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync()
	{
		await _quick.DisposeAsync();

		if (_client is { })
		{
			try
			{
				await _client.DisposeAsync();
			}
			catch
			{
				/* best-effort */
			}

			_client = null;
		}

		_transport = null;
	}

	private static MoveScore ScoreFromResult(SearchResult result)
	{
		if (result is { HasMate: true, MateScore: { } })
			return MoveScore.FromMate(result.MateScore.Value);

		int? cp = result.BestCpScore ?? result.PrincipalVariations.FirstOrDefault().ScoreCp;
		return cp.HasValue ? MoveScore.FromCp(cp.Value) : default;
	}

	private async Task<Move?> EvaluateMoveAtRootAsync(
		Fen               fen,
		string            move,
		uint              perMoveDepth,
		CancellationToken ct)
	{
		var fenKey   = fen.ToString();
		var cacheKey = (fenKey, move, perMoveDepth);

		MoveScore    score;
		MoveAnalysis analysis;
		if (_moveEvalCache.TryGetValue(cacheKey, out var cached))
		{
			// Recompute analysis based on current board, reuse cached score and flags
			score    = cached.Score;
			analysis = MoveAnalysis.Analyze(move, BoardState.FromFen(fen).Value, score, cached.IsStalemate);
			return new Move(move, analysis);
		}

		// Ensure engine is at root position and restrict search to this single move
		await _client.SetPositionAsync(fen, [move], ct).ConfigureAwait(false);
		var result = await _client.GoAsync(new() { Depth = perMoveDepth, SearchMoves = [move] }, ct)
								  .ConfigureAwait(false);

		score = ScoreFromResult(result);

		// Determine terminal positions using helper methods for consistency.
		bool isMate      = await IsCheckmateAsync(fen, move, ct).ConfigureAwait(false);
		bool isStalemate = !isMate && await IsStalemateAsync(fen, move, ct).ConfigureAwait(false);

		// Normalize mate scoring so MoveAnalysis flags mate/check reliably.
		if (isMate && score.ScoreMate is not -1)
			score = MoveScore.FromMate(-1);

		// Store normalized result in cache
		_moveEvalCache[cacheKey] = (score, isMate, isStalemate);

		analysis = MoveAnalysis.Analyze(move, BoardState.FromFen(fen).Value, score, isStalemate);
		return new Move(move, analysis);
	}

	private void ClearCaches()
	{
		_moveEvalCache.Clear();
		_isMateCache.Clear();
		_isStalemateCache.Clear();
	}

	private void EnsureStarted()
	{
		if (!_started || _client is null)
			throw new InvalidOperationException(
				"MoveClassificationEngine must be started by calling StartAsync() before use.");
	}
}
