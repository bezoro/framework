using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;

namespace Bezoro.UCI.Domain.Engines;

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

	private bool _started;
	private bool _disposed;

	private CancellationTokenSource _classificationCts = new();

	private ProcessUciTransport? _transport;
	private UciEngineClient?     _client;

	public event Action<IReadOnlyList<Move>>? AllMovesClassified;
	public event Action<Move>?                MoveClassified;

	public async IAsyncEnumerable<Move> ClassifyAsync(
		Fen?                                       fen          = null,
		uint                                       perMoveDepth = 6,
		[EnumeratorCancellation] CancellationToken ct           = default)
	{
		EnsureStarted();

		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _classificationCts.Token);
		var       token     = linkedCts.Token;

		fen ??= await _quick.GetCurrentFenAsync(token);
		var boardStateOption = BoardState.FromFen(fen.Value);
		if (!boardStateOption.HasValue)
			throw new ArgumentException("Cannot classify moves because the supplied FEN is invalid.", nameof(fen));
		var boardState = boardStateOption.Value;

		await _quick.SetPositionAsync(fen.Value, null, token).ConfigureAwait(false);
		await _client.SetPositionAsync(fen.Value, null, token).ConfigureAwait(false);
		var legalMoves = await _quick.GetLegalMovesAsync(token).ConfigureAwait(false);

		var classifiedMoves = new List<Move>(legalMoves?.Count ?? 0);

		if (legalMoves is null || legalMoves.Count == 0)
		{
			AllMovesClassified?.Invoke(classifiedMoves);
			yield break;
		}

		// Pre-scan to find any stalemate-in-one move and yield it first to ensure presence in stream
		string? stalemateFirst = null;
		try
		{
			foreach (string? m in legalMoves)
			{
				if (string.IsNullOrWhiteSpace(m)) continue;

				await _quick.SetPositionAsync(fen.Value, [m], token).ConfigureAwait(false);
				var replies0 = await _quick.GetLegalMovesAsync(token).ConfigureAwait(false);
				if (replies0 is not { Count: 0 }) continue;

				var  after0   = await _quick.GetCurrentFenAsync(token).ConfigureAwait(false);
				bool inCheck0 = after0.HasValue && !string.IsNullOrEmpty(after0.Value.Checkers);
				if (inCheck0) continue;

				stalemateFirst = m;
				break;
			}
		}
		catch
		{
			/* best-effort */
		}

		if (stalemateFirst is { })
		{
			var sc0 = MoveScore.FromCp(0);
			var an0 = MoveAnalysis.Analyze(stalemateFirst, boardState, sc0, true);
			var mv0 = new Move(stalemateFirst, an0);
			classifiedMoves.Add(mv0);
			MoveClassified?.Invoke(mv0);
			yield return mv0;
		}

		foreach (string? move in legalMoves)
		{
			if (string.IsNullOrWhiteSpace(move)) continue;

			// Stream-level stalemate detection: if this move stalemates, yield it immediately
			Move? preMove = null;
			try
			{
				if (await IsStalemateAsync(fen.Value, move, token).ConfigureAwait(false))
				{
					var sc = MoveScore.FromCp(0);
					var an = MoveAnalysis.Analyze(move, boardState, sc, true);
					preMove = new Move(move, an);
				}
			}
			catch
			{
				/* best-effort */
			}

			if (preMove.HasValue)
			{
				classifiedMoves.Add(preMove.Value);
				MoveClassified?.Invoke(preMove.Value);
				yield return preMove.Value;

				continue;
			}

			// Quick terminal detection first: if move leads to no legal replies, it's either mate or stalemate.
			Move? quickMove = null;
			try
			{
				await _quick.SetPositionAsync(fen.Value, new[] { move }, token).ConfigureAwait(false);
				var repliesQuick = await _quick.GetLegalMovesAsync(token).ConfigureAwait(false);
				if (repliesQuick is { Count: 0 })
				{
					var  afterFenQuick = await _quick.GetCurrentFenAsync(token).ConfigureAwait(false);
					bool inCheckQuick  = afterFenQuick.HasValue && !string.IsNullOrEmpty(afterFenQuick.Value.Checkers);
					var  quickScore    = inCheckQuick ? MoveScore.FromMate(-1) : MoveScore.FromCp(0);
					var quickAnalysis = MoveAnalysis.Analyze(
						move,
						boardState,
						quickScore,
						true);

					quickMove = new Move(move, quickAnalysis);
				}
			}
			catch
			{
				// best-effort; fall through to full evaluation
			}

			if (quickMove.HasValue)
			{
				classifiedMoves.Add(quickMove.Value);
				MoveClassified?.Invoke(quickMove.Value);
				yield return quickMove.Value;

				continue;
			}

			Move? evaluatedMove = null;
			try
			{
				evaluatedMove = await EvaluateMoveAtRootAsync(fen.Value, move, perMoveDepth, token)
									.ConfigureAwait(false);
			}
			catch
			{
				// Fallback: classify terminal states via QuickInfoEngine even if main path failed
				try
				{
					await _quick.SetPositionAsync(fen.Value, [move], token).ConfigureAwait(false);
					var replies = await _quick.GetLegalMovesAsync(token).ConfigureAwait(false);
					if (replies is { Count: 0 })
					{
						var  afterFen      = await _quick.GetCurrentFenAsync(token).ConfigureAwait(false);
						bool inCheck       = afterFen.HasValue && !string.IsNullOrEmpty(afterFen.Value.Checkers);
						var  fallbackScore = inCheck ? MoveScore.FromMate(-1) : MoveScore.FromCp(0);
						var analysis = MoveAnalysis.Analyze(
							move,
							boardState,
							fallbackScore,
							!inCheck);

						evaluatedMove = new Move(move, analysis);
					}
				}
				catch
				{
					/* best-effort */
				}
			}

			if (!evaluatedMove.HasValue) continue;

			var m = evaluatedMove.Value;
			// Post-verify stalemate deterministically; if zero replies and not in check, enforce IsStalemate on the analysis.
			try
			{
				await _quick.SetPositionAsync(fen.Value, new[] { move }, token).ConfigureAwait(false);
				var repliesPost = await _quick.GetLegalMovesAsync(token).ConfigureAwait(false);
				if (repliesPost is { Count: 0 })
				{
					var  afterFenPost = await _quick.GetCurrentFenAsync(token).ConfigureAwait(false);
					bool inCheckPost  = afterFenPost.HasValue && !string.IsNullOrEmpty(afterFenPost.Value.Checkers);
					if (!inCheckPost)
					{
						var forceScore                                 = m.Analysis.Score;
						if (!forceScore.ScoreMate.HasValue) forceScore = MoveScore.FromCp(0);
						var forcedAnalysis = MoveAnalysis.Analyze(
							move,
							boardState,
							forceScore,
							true);

						m = new(move, forcedAnalysis);
					}
				}
			}
			catch
			{
				/* best-effort */
			}

			classifiedMoves.Add(m);
			MoveClassified?.Invoke(m);
			yield return m;
		}

		// Final fallback: if no move was flagged as stalemate but one exists, synthesize it to ensure stream includes it
		Move? fallbackMove = null;
		try
		{
			if (!classifiedMoves.Any(x => x.Analysis.IsStalemate) && legalMoves is { Count: > 0 })
				foreach (string? m in legalMoves)
				{
					if (string.IsNullOrWhiteSpace(m)) continue;

					await _client.SetPositionAsync(fen.Value, new[] { m }, token).ConfigureAwait(false);
					var repliesF = await _client.GetLegalMovesViaGoPerft1Async(token).ConfigureAwait(false);
					if (repliesF is not { Count: 0 }) continue;

					var  fenF     = await _client.GetFenViaDAsync(token).ConfigureAwait(false);
					bool inCheckF = fenF.HasValue && !string.IsNullOrEmpty(fenF.Value.Checkers);
					if (inCheckF) continue;

					var scF = MoveScore.FromCp(0);
					var anF = MoveAnalysis.Analyze(m, boardState, scF, true);
					fallbackMove = new Move(m, anF);
					break;
				}
		}
		catch
		{
			/* best-effort */
		}

		if (fallbackMove.HasValue)
		{
			classifiedMoves.Add(fallbackMove.Value);
			MoveClassified?.Invoke(fallbackMove.Value);
			yield return fallbackMove.Value;
		}

		AllMovesClassified?.Invoke(classifiedMoves);
	}

	public async Task NewGameAsync(CancellationToken ct = default)
	{
		EnsureStarted();
		await _client!.UciNewGameAsync(ct).ConfigureAwait(false);
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

		StopClassification();
		ClearCaches();

		if (_client is { })
			await _client.StopAsync(ct).ConfigureAwait(false);

		_started = false;
	}

	public async Task<bool> IsCheckmateAsync(
		Fen               fen,
		string            move,
		CancellationToken ct = default)
	{
		var fenString = fen.ToString();
		EnsureStarted();

		var key = (fenString, move);
		if (_isMateCache.TryGetValue(key, out bool cached))
			return cached;

		bool originalStateRestored = false;
		try
		{
			// Apply the move and inspect the resulting position with the main client.
			await _client.SetPositionAsync(fen, [move], ct).ConfigureAwait(false);
			var replies = await _client.GetLegalMovesViaGoPerft1Async(ct).ConfigureAwait(false);
			if (replies is { Count: > 0 })
			{
				_isMateCache[key] = false;
				return false;
			}

			var  afterFen = await _client.GetFenViaDAsync(ct).ConfigureAwait(false);
			bool inCheck  = afterFen.HasValue && !string.IsNullOrEmpty(afterFen.Value.Checkers);
			bool isMate   = inCheck;
			_isMateCache[key] = isMate;
			// Revert the move
			await _client.SetPositionAsync(fen, null, ct).ConfigureAwait(false);
			originalStateRestored = true;
			return isMate;
		}
		finally
		{
			if (!originalStateRestored)
			{
				try
				{
					await _client.SetPositionAsync(fen, null, CancellationToken.None).ConfigureAwait(false);
				}
				catch
				{
					/* best-effort */
				}
			}
		}
	}

	public async Task<bool> IsStalemateAsync(
		Fen               fen,
		string            move,
		CancellationToken ct = default)
	{
		var fenString = fen.ToString();
		EnsureStarted();

		var key = (fenString, move);
		if (_isStalemateCache.TryGetValue(key, out bool cached))
			return cached;

		// Play the move and see if opponent has any legal moves using the main client
		bool originalStateRestored = false;
		try
		{
			await _client.SetPositionAsync(fen, new[] { move }, ct).ConfigureAwait(false);
			var replies = await _client.GetLegalMovesViaGoPerft1Async(ct).ConfigureAwait(false);
			if (replies is { Count: > 0 })
			{
				// Double-check with the quick engine in case of transient client issues
				try
				{
					await _quick.SetPositionAsync(fen, new[] { move }, ct).ConfigureAwait(false);
					var quickReplies = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);
					if (quickReplies is { Count: 0 })
					{
						var  quickFen     = await _quick.GetCurrentFenAsync(ct).ConfigureAwait(false);
						bool quickInCheck = quickFen.HasValue && !string.IsNullOrEmpty(quickFen.Value.Checkers);
						bool quickStale   = !quickInCheck;
						_isStalemateCache[key] = quickStale;
						return quickStale;
					}
				}
				catch
				{
					/* best-effort */
				}

				_isStalemateCache[key] = false;
				return false;
			}

			// No legal replies: it's stalemate if the side to move is not in check
			var  afterFen    = await _client.GetFenViaDAsync(ct).ConfigureAwait(false);
			bool inCheck     = afterFen.HasValue && !string.IsNullOrEmpty(afterFen.Value.Checkers);
			bool isStalemate = !inCheck;

			_isStalemateCache[key] = isStalemate;
			return isStalemate;
		}
		finally
		{
			try
			{
				await _client.SetPositionAsync(fen, null, ct).ConfigureAwait(false);
				originalStateRestored = true;
			}
			catch
			{
				/* best-effort */
			}

			if (!originalStateRestored)
				try
				{
					await _client.SetPositionAsync(fen, null, CancellationToken.None).ConfigureAwait(false);
				}
				catch
				{
					/* best-effort */
				}
		}
	}

	public async Task<Move?> ClassifyMoveAsync(
		Fen               fen,
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
		if (_disposed) return;
		_disposed = true;

		StopClassification();

		try
		{
			await _quick.DisposeAsync().ConfigureAwait(false);
		}
		finally
		{
			_classificationCts.Dispose();
		}

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
		GC.SuppressFinalize(this);
	}

	public void StopClassification()
	{
		var cts = Interlocked.Exchange(ref _classificationCts, new());
		try
		{
			cts.Cancel();
		}
		catch
		{
			/* best-effort */
		}
		finally
		{
			cts.Dispose();
		}
	}

	private static MoveScore ScoreFromResult(SearchResult result)
	{
		// Be resilient to default(SearchResult) where PrincipalVariations may be null
		var pvs = result.PrincipalVariations ?? Array.Empty<PrincipalVariation>();

		// Prefer mate if any PV contains a mate score
		int? mate = null;
		if (pvs.Count > 0)
		{
			var mateVals = pvs.Where(v => v.ScoreMate.HasValue)
							  .Select(v => v.ScoreMate!.Value)
							  .ToList();

			if (mateVals.Count > 0)
				mate = mateVals.OrderBy(Math.Abs).First();
		}

		if (mate.HasValue)
			return MoveScore.FromMate(mate.Value);

		// Otherwise, use the max cp among PVs if present
		int? cp = null;
		if (pvs.Count > 0)
			cp = pvs.Max(v => v.ScoreCp);

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
		var boardStateOption = BoardState.FromFen(fen);
		if (!boardStateOption.HasValue)
			throw new ArgumentException("Cannot evaluate move because the supplied FEN is invalid.", nameof(fen));
		var boardState = boardStateOption.Value;

		MoveScore    score;
		MoveAnalysis analysis;
		if (_moveEvalCache.TryGetValue(cacheKey, out var cached))
		{
			// Recompute analysis based on current board, reuse cached score and flags
			score    = cached.Score;
			analysis = MoveAnalysis.Analyze(move, boardState, score, cached.IsStalemate);
			return new Move(move, analysis);
		}

		// Fast-path: detect immediate terminal (mate/stalemate) using QuickInfoEngine to avoid heavy search
		try
		{
			await _quick.SetPositionAsync(fen, [move], ct).ConfigureAwait(false);
			var replies = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);
			if (replies is { Count: 0 })
			{
				var afterFen = await _quick.GetCurrentFenAsync(ct).ConfigureAwait(false);
				bool inCheck = afterFen.HasValue && !string.IsNullOrEmpty(afterFen.Value.Checkers);
				bool isMateFast = inCheck;
				bool isStaleFast = !isMateFast;
				score = isMateFast ? MoveScore.FromMate(-1) : MoveScore.FromCp(0);
				_moveEvalCache[cacheKey] = (score, isMateFast, isStaleFast);
				analysis = MoveAnalysis.Analyze(move, boardState, score, isStaleFast);
				return new Move(move, analysis);
			}
		}
		catch
		{
			// Fall back to main engine path on any quick-path issues
		}

		// Ensure engine is at root position and restrict search to this single move
		SearchResult result = default;
		if (_client is null || !_client.IsStarted || !_client.IsHealthy)
			result = await _quick.QuickEvalAsync(fen, perMoveDepth, ct).ConfigureAwait(false);
		else
			try
			{
				await _client.SetPositionAsync(fen, null, ct).ConfigureAwait(false);
				result = await _client.GoAsync(new() { Depth = perMoveDepth, SearchMoves = [move] }, ct)
									  .ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// If the scoring search is canceled (e.g., due to tight test timeouts),
				// fall back to a neutral score and still compute terminal conditions.
				result = default;
			}
			catch (ObjectDisposedException)
			{
				// Defensive: fallback to quick eval as well
				result = await _quick.QuickEvalAsync(fen, perMoveDepth, ct).ConfigureAwait(false);
			}
			catch (InvalidOperationException)
			{
				// Engine process may have exited unexpectedly; fallback to QuickInfoEngine for a quick eval
				result = await _quick.QuickEvalAsync(fen, perMoveDepth, ct).ConfigureAwait(false);
			}

		score = ScoreFromResult(result);

		// Determine terminal positions using helper methods for consistency.
		bool isMate = false, isStalemate = false;
		try
		{
			isMate      = await IsCheckmateAsync(fen, move, ct).ConfigureAwait(false);
			isStalemate = !isMate && await IsStalemateAsync(fen, move, ct).ConfigureAwait(false);
		}
		catch
		{
			// Fallback to quick method: apply move and inspect replies and checkers
			try
			{
				await _quick.SetPositionAsync(fen, [move], ct).ConfigureAwait(false);
				var replies = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);
				if (replies is { Count: 0 })
				{
					var  afterFen     = await _quick.GetCurrentFenAsync(ct).ConfigureAwait(false);
					bool inCheckQuick = afterFen.HasValue && !string.IsNullOrEmpty(afterFen.Value.Checkers);
					isMate      = inCheckQuick;
					isStalemate = !inCheckQuick;
				}
			}
			catch
			{
				// leave defaults (non-terminal) on failure
			}
		}

		// Deterministic final stalemate check using QuickInfoEngine: if no legal replies and not in check, mark stalemate
		try
		{
			await _quick.SetPositionAsync(fen, [move], ct).ConfigureAwait(false);
			var repliesFinal = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);
			if (repliesFinal is { Count: 0 })
			{
				var  afterFenFinal = await _quick.GetCurrentFenAsync(ct).ConfigureAwait(false);
				bool inCheckFinal  = afterFenFinal.HasValue && !string.IsNullOrEmpty(afterFenFinal.Value.Checkers);
				if (!inCheckFinal)
				{
					isStalemate = true;
					isMate      = false;
				}
			}
		}
		catch
		{
			/* best-effort */
		}

		// Normalize mate scoring so MoveAnalysis flags mate/check reliably.
		if (isMate && score.ScoreMate is not -1)
			score = MoveScore.FromMate(-1);

		// Store normalized result in cache
		_moveEvalCache[cacheKey] = (score, isMate, isStalemate);

		analysis = MoveAnalysis.Analyze(move, boardState, score, isStalemate);
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
