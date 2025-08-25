using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain;

namespace Bezoro.UCI.API;

public sealed class UciCoordinator : IAsyncDisposable
{
	private readonly Dictionary<string, Move> _classifiedMovesForCurrent = new();
	private readonly List<string>             _playedMoves               = [];
	private readonly MoveClassificationEngine _classifier;
	private readonly object                   _cacheLock = new();
	private readonly PonderEngine             _ponder;
	private readonly QuickInfoEngine          _quick;
	private readonly TimeSpan                 _ponderBestMoveInterval;

	private CancellationTokenSource? _bestCts;
	private CancellationTokenSource? _classificationCts;

	private Fen? _initialFen;

	private volatile int _ponderPulseActiveFlag;

	private IReadOnlyCollection<string>? _currentLegalMoves;

	private PrincipalVariation? _bestTopPv;
	private PrincipalVariation? _ponderTopPv;

	private string? _currentPositionKey;
	private string? _lastPonderKey;
	private string? _latestPonderBest;
	private string? _latestPonderPonder;

	private Timer? _ponderPulseTimer;

	public event Action<PrincipalVariation>?          BestLineUpdated;
	public event Action<string, string>?              BestMoveUpdated;
	public event Action<IReadOnlyCollection<string>>? LegalMovesUpdated;
	public event Action<string>?                      MoveClassificationCompleted;
	public event Action<string, Move>?                MoveClassified;
	public event Action<string, string>?              PonderBestMove;
	public event Action<PrincipalVariation>?          PonderInfo;

	public UciCoordinator(
		string               enginePath,
		IEnumerable<string>? args                   = null,
		string?              workingDirectory       = null,
		TimeSpan?            ponderBestMoveInterval = null)
	{
		_quick                  = new(enginePath, args, workingDirectory);
		_ponder                 = new(enginePath, args, workingDirectory);
		_classifier             = new(enginePath, args, workingDirectory);
		_ponderBestMoveInterval = ponderBestMoveInterval ?? TimeSpan.FromSeconds(5);

		_ponder.InfoPv += OnPonderInfo;
		_ponder.BestMove += (b, p) =>
		{
			PonderBestMove?.Invoke(b, p);
			BestMoveUpdated?.Invoke(b, p);
		};
	}

	// Optional snapshot of current legal moves
	public IReadOnlyCollection<string>? CurrentLegalMoves
	{
		get
		{
			lock (_cacheLock)
			{
				return _currentLegalMoves;
			}
		}
	}

	public IAsyncEnumerable<Move> ClassifyMovesAsync(
		Fen               fen,
		uint              perMoveDepth = 6,
		CancellationToken ct           = default)
		=> _classifier.ClassifyAsync(fen, perMoveDepth, ct);

	// Apply a move: updates state, broadcasts legal moves, restarts searches and classification.
	public async Task MoveMadeAsync(string move, CancellationToken ct = default)
	{
		// Ensure legal-moves cache
		var legal = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);
		if (legal is null || !legal.Contains(move))
			throw new ArgumentException($"Move '{move}' is not legal in the current position.");

		Fen fen;
		lock (_cacheLock)
		{
			if (_initialFen is null)
				throw new InvalidOperationException("Initial position not set. Call StartAsync(fen, moves) first.");

			_playedMoves.Add(move);
			fen = _initialFen.Value;
		}

		await UpdatePositionAsync(fen, _playedMoves, ct).ConfigureAwait(false);
	}

	public async Task NewGameAsync(CancellationToken ct = default)
	{
		await Task.WhenAll(
			_classifier.NewGameAsync(ct),
			_ponder.NewGameAsync(ct),
			_quick.NewGameAsync(ct)
		).ConfigureAwait(false);

		StopPonderPulseTimer();

		// Clear cached state for new game and cancel any in-flight classification
		var toCancel = CaptureAndClearStateLocked(true);
		toCancel?.Cancel();
		toCancel?.Dispose();

		lock (_cacheLock)
		{
			_initialFen = null;
			_playedMoves.Clear();
			_currentLegalMoves = null;
			_bestTopPv         = null;
			_ponderTopPv       = null;
		}
	}

	// Starts engines; does not set a position
	public async Task StartAsync(CancellationToken ct = default)
	{
		await Task.WhenAll(
			_quick.StartAsync(ct),
			_ponder.StartAsync(ct),
			_classifier.StartAsync(ct)
		).ConfigureAwait(false);

		await _ponder.SetOptionAsync("Threads", "2", ct).ConfigureAwait(false);
		await _ponder.SetOptionAsync("MultiPv", "1", ct).ConfigureAwait(false);

		// Clear cached state at the start of a session
		_ = CaptureAndClearStateLocked(true);

		lock (_cacheLock)
		{
			_initialFen = null;
			_playedMoves.Clear();
			_currentLegalMoves = null;
			_bestTopPv         = null;
			_ponderTopPv       = null;
		}
	}

	// Starts engines and immediately sets position, updates legal moves, and starts searches/classification
	public async Task StartAsync(
		Fen                  initialFen,
		IEnumerable<string>? playedMoves = null,
		CancellationToken    ct          = default)
	{
		await StartAsync(ct).ConfigureAwait(false);

		lock (_cacheLock)
		{
			_initialFen = initialFen;
			_playedMoves.Clear();
			if (playedMoves is { })
				_playedMoves.AddRange(playedMoves.Where(m => !string.IsNullOrWhiteSpace(m)));
		}

		await UpdatePositionAsync(initialFen, playedMoves, ct).ConfigureAwait(false);
	}

	// Starts or restarts an infinite best-move search for the current position (derived from Quick engine FEN).
	public async Task StartBestAsync(CancellationToken ct = default)
	{
		_bestCts?.Cancel();
		_bestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

		var fen = await _quick.GetCurrentFenAsync(_bestCts.Token).ConfigureAwait(false);
		if (fen is null) return;

		await _ponder.StartSearchAsync(fen.Value, null, false, _bestCts.Token).ConfigureAwait(false);
	}

	public Task StartPonderAsync(Fen fen, IEnumerable<string>? playedMoves, CancellationToken ct = default)
	{
		string key = BuildPositionKey(fen, playedMoves);
		bool   identical;
		lock (_cacheLock)
		{
			identical = string.Equals(_lastPonderKey, key, StringComparison.Ordinal);
			if (!identical)
			{
				_lastPonderKey      = key;
				_latestPonderBest   = null;
				_latestPonderPonder = null;
				_ponderTopPv        = null;
			}
		}

		// Start or refresh periodic PonderBestMove pulses without restarting the search
		StartPonderPulseTimer();

		// If already pondering the same position, don't restart the engine search
		return identical ? Task.CompletedTask : _ponder.StartPonderAsync(fen, playedMoves, ct);
	}

	public async Task StopAsync(CancellationToken ct = default)
	{
		// Stop searches first
		await StopBestAsync(ct).ConfigureAwait(false);
		await StopPonderAsync(ct).ConfigureAwait(false);

		// Stop engines
		await Task.WhenAll(
			_classifier.StopAsync(ct),
			_ponder.StopAsync(ct),
			_quick.StopAsync(ct)
		).ConfigureAwait(false);

		StopPonderPulseTimer();

		// Clear cached state on stop and cancel in-flight classification
		var toCancel = CaptureAndClearStateLocked(true);
		toCancel?.Cancel();
		toCancel?.Dispose();

		lock (_cacheLock)
		{
			_initialFen = null;
			_playedMoves.Clear();
			_currentLegalMoves = null;
			_bestTopPv         = null;
			_ponderTopPv       = null;
		}
	}

	public async Task StopBestAsync(CancellationToken ct = default)
	{
		_bestCts?.Cancel();
		try
		{
			await _ponder.StopSearchAsync(ct).ConfigureAwait(false);
		}
		catch
		{
			/* best-effort */
		}
	}

	public async Task StopPonderAsync(CancellationToken ct = default)
	{
		await _ponder.StopPonderAsync(ct).ConfigureAwait(false);

		// Stop periodic pulses
		StopPonderPulseTimer();

		// Clear cached ponder key so future identical requests are not skipped
		lock (_cacheLock)
		{
			_lastPonderKey = null;
		}
	}

	public async Task UpdatePositionAsync(
		Fen                  fen,
		IEnumerable<string>? playedMoves,
		CancellationToken    ct = default)
	{
		// Build/track current position key and short-circuit if unchanged
		string key = BuildPositionKey(fen, playedMoves);
		lock (_cacheLock)
		{
			if (string.Equals(_currentPositionKey, key, StringComparison.Ordinal))
				return;

			_currentPositionKey = key;
			_initialFen         = fen;
			_playedMoves.Clear();
			if (playedMoves is { })
				_playedMoves.AddRange(playedMoves.Where(m => !string.IsNullOrWhiteSpace(m)));

			// Reset best/ponder tops for the new position
			_bestTopPv   = null;
			_ponderTopPv = null;
		}

		// Stop previous searches
		await StopBestAsync(ct).ConfigureAwait(false);
		await StopPonderAsync(ct).ConfigureAwait(false);

		// Prepare new classification context (with linked CTS)
		PrepareNewClassificationContext(key, ct, out var oldCts, out var localCts);

		// Cancel prior classification (outside lock)
		oldCts?.Cancel();
		oldCts?.Dispose();

		// Ensure quick engine is at the new position for fast legal moves
		await _quick.SetPositionAsync(fen, playedMoves, ct).ConfigureAwait(false);

		// Update and broadcast legal moves immediately
		var moves = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);
		lock (_cacheLock)
		{
			_currentLegalMoves = moves;
		}

		try
		{
			LegalMovesUpdated?.Invoke(moves);
		}
		catch
		{
			/* best-effort */
		}

		// Start pondering and best searches for the new position (fire-and-forget)
		_ = StartPonderAsync(fen, playedMoves, ct);
		_ = StartBestAsync(ct);

		// Kick off background classification pipeline
		StartClassificationPipeline(fen, key, localCts);
	}

	public async Task<bool> IsMatchFinishedAsync(CancellationToken ct = default)
	{
		var legalMoves = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);
		return legalMoves.Count == 0;
	}

	public Task<Fen?> GetCurrentFenAsync(CancellationToken ct = default) =>
		_quick.GetCurrentFenAsync(ct);

	public Task<IReadOnlyCollection<string>> GetLegalMovesAsync(CancellationToken ct = default) =>
		_quick.GetLegalMovesAsync(ct);

	/// <summary>
	///     Returns the fast-path legal moves and any per-move classifications that are ready so far.
	///     Bare moves are provided as ParsedMove.
	/// </summary>
	public async Task<MovesSnapshot> GetLegalMovesWithClassificationsAsync(CancellationToken ct = default)
	{
		var legalStrings = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);
		var legal        = ToParsedMoves(legalStrings);
		var classified   = SnapshotClassifiedMoves();
		return new(legal, classified);
	}

	public async ValueTask DisposeAsync()
	{
		// Stop any ongoing ponder pulses
		StopPonderPulseTimer();

		// Cancel any background classification
		var toCancel = CaptureAndClearStateLocked(true);
		toCancel?.Cancel();
		toCancel?.Dispose();

		await _classifier.DisposeAsync();
		await _ponder.DisposeAsync();
		await _quick.DisposeAsync();
	}

	private static int ComparePv(PrincipalVariation lhs, PrincipalVariation rhs)
	{
		// Mates outrank CP; higher mate number is better (engine's perspective for side to move)
		bool lHasMate = lhs.ScoreMate.HasValue;
		bool rHasMate = rhs.ScoreMate.HasValue;

		if (lHasMate && !rHasMate) return 1;
		if (!lHasMate && rHasMate) return -1;

		if (lHasMate && rHasMate)
		{
			int cmpMate = lhs.ScoreMate!.Value.CompareTo(rhs.ScoreMate!.Value);
			if (cmpMate != 0) return cmpMate;

			// Fallback: depth
			int cmpDepth = lhs.Depth.CompareTo(rhs.Depth);
			if (cmpDepth != 0) return cmpDepth;

			return lhs.SelDepth.CompareTo(rhs.SelDepth);
		}

		// Compare CP
		int lcp = lhs.ScoreCp ?? int.MinValue;
		int rcp = rhs.ScoreCp ?? int.MinValue;

		int cmp = lcp.CompareTo(rcp);
		if (cmp != 0) return cmp;

		// Tie-breaker: deeper is better
		int cmpD = lhs.Depth.CompareTo(rhs.Depth);
		if (cmpD != 0) return cmpD;

		return lhs.SelDepth.CompareTo(rhs.SelDepth);
	}

	private static List<ParsedMove> ToParsedMoves(IReadOnlyCollection<string> legalStrings)
	{
		var legal = new List<ParsedMove>(legalStrings.Count);
		foreach (string s in legalStrings)
			legal.Add(ParsedMove.FromNotation(s));

		return legal;
	}

	private static string BuildPositionKey(Fen fen, IEnumerable<string>? playedMoves)
	{
		string movesJoined = playedMoves is null ? string.Empty : string.Join(' ', playedMoves);
		return $"{fen}|{movesJoined}";
	}

	private bool TryGetLatestPonder(out string? best, out string? ponder)
	{
		lock (_cacheLock)
		{
			bool shouldFire = _ponderPulseTimer is { } &&
							  !string.IsNullOrEmpty(_lastPonderKey) &&
							  !string.IsNullOrEmpty(_latestPonderBest);

			best   = _latestPonderBest;
			ponder = _latestPonderPonder ?? string.Empty;
			return shouldFire;
		}
	}

	private CancellationTokenSource? CaptureAndClearStateLocked(bool clearPonderKey)
	{
		CancellationTokenSource? toCancel;
		lock (_cacheLock)
		{
			toCancel            = _classificationCts;
			_classificationCts  = null;
			_currentPositionKey = null;
			_classifiedMovesForCurrent.Clear();
			if (clearPonderKey)
				_lastPonderKey = null;
		}

		return toCancel;
	}

	private IReadOnlyDictionary<ParsedMove, Move> SnapshotClassifiedMoves()
	{
		lock (_cacheLock)
		{
			var dict = new Dictionary<ParsedMove, Move>(_classifiedMovesForCurrent.Count);
			foreach (var kvp in _classifiedMovesForCurrent)
				dict[ParsedMove.FromNotation(kvp.Key)] = kvp.Value;

			return dict;
		}
	}


	private void OnPonderInfo(PrincipalVariation pv)
	{
		try
		{
			PonderInfo?.Invoke(pv);
		}
		catch
		{
			/* best-effort */
		}

		var                 updatedPonder = false;
		var                 updatedBest   = false;
		PrincipalVariation? topPonder;
		PrincipalVariation? topBest;
		lock (_cacheLock)
		{
			if (_ponderTopPv is null || ComparePv(pv, _ponderTopPv.Value) > 0)
			{
				_ponderTopPv        = pv;
				_latestPonderBest   = pv.Moves is { Count: > 0 } ? pv.Moves[0] : _latestPonderBest;
				_latestPonderPonder = pv.Moves is { Count: > 1 } ? pv.Moves[1] : _latestPonderPonder;
				updatedPonder       = true;
			}

			if (_bestTopPv is null || ComparePv(pv, _bestTopPv.Value) > 0)
			{
				_bestTopPv  = pv;
				updatedBest = true;
			}

			topPonder = _ponderTopPv;
			topBest   = _bestTopPv;
		}

		if (updatedPonder && topPonder is { } tp)
		{
			// Emit immediate ponder best/ponder derived from top PV
			string best   = tp.Moves.Count > 0 ? tp.Moves[0] : string.Empty;
			string ponder = tp.Moves.Count > 1 ? tp.Moves[1] : string.Empty;
			try
			{
				PonderBestMove?.Invoke(best, ponder);
			}
			catch
			{
				/* best-effort */
			}
		}

		if (updatedBest && topBest is { } tb)
		{
			try
			{
				BestLineUpdated?.Invoke(tb);
			}
			catch
			{
				/* best-effort */
			}

			string best   = tb.Moves.Count > 0 ? tb.Moves[0] : string.Empty;
			string ponder = tb.Moves.Count > 1 ? tb.Moves[1] : string.Empty;
			try
			{
				BestMoveUpdated?.Invoke(best, ponder);
			}
			catch
			{
				/* best-effort */
			}
		}
	}

	private void PonderPulseCallback(object? _)
	{
		// Avoid re-entrant callbacks if the action takes longer than the interval
		if (Interlocked.Exchange(ref _ponderPulseActiveFlag, 1) == 1) return;

		try
		{
			if (TryGetLatestPonder(out string? best, out string? ponder) && best is { })
				PonderBestMove?.Invoke(best, ponder ?? string.Empty);
		}
		catch
		{
			// best-effort: swallow timer exceptions
		}
		finally
		{
			Volatile.Write(ref _ponderPulseActiveFlag, 0);
		}
	}

	private void PrepareNewClassificationContext(
		string                       positionKey,
		CancellationToken            ct,
		out CancellationTokenSource? oldCts,
		out CancellationTokenSource  newCts)
	{
		lock (_cacheLock)
		{
			oldCts = _classificationCts;
			var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
			_classificationCts  = linked;
			_currentPositionKey = positionKey;
			_classifiedMovesForCurrent.Clear();
			newCts = linked;
		}
	}

	private void StartClassificationPipeline(Fen fen, string positionKey, CancellationTokenSource localCts)
	{
		_ = Task.Run(
			async () =>
			{
				try
				{
					await foreach (var move in _classifier.ClassifyAsync(fen, 6, localCts.Token).ConfigureAwait(false))
					{
						bool stillCurrent;
						lock (_cacheLock)
						{
							stillCurrent = string.Equals(_currentPositionKey, positionKey, StringComparison.Ordinal) &&
										   !localCts.IsCancellationRequested;

							if (stillCurrent)
								_classifiedMovesForCurrent[move.Notation] = move;
						}

						if (stillCurrent)
							MoveClassified?.Invoke(positionKey, move);
						else
							break;
					}

					bool fireCompleted;
					lock (_cacheLock)
					{
						fireCompleted = string.Equals(_currentPositionKey, positionKey, StringComparison.Ordinal) &&
										!localCts.IsCancellationRequested;
					}

					if (fireCompleted)
						MoveClassificationCompleted?.Invoke(positionKey);
				}
				catch (OperationCanceledException)
				{
					// Expected when a new position cancels the pipeline
				}
				catch
				{
					// Best-effort background processing; swallow unexpected exceptions
				}
			},
			CancellationToken.None);
	}

	private void StartPonderPulseTimer()
	{
		lock (_cacheLock)
		{
			_ponderPulseTimer?.Dispose();
			_ponderPulseActiveFlag = 0;
			_ponderPulseTimer = new(
				PonderPulseCallback,
				null,
				_ponderBestMoveInterval,
				_ponderBestMoveInterval);
		}
	}

	private void StopPonderPulseTimer()
	{
		Timer? timer;
		lock (_cacheLock)
		{
			timer                  = _ponderPulseTimer;
			_ponderPulseTimer      = null;
			_latestPonderBest      = null;
			_latestPonderPonder    = null;
			_ponderPulseActiveFlag = 0;
		}

		timer?.Dispose();
	}
}
