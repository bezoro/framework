using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain;

namespace Bezoro.UCI.API;

public sealed class UciCoordinator : IAsyncDisposable
{
	private readonly Dictionary<string, Move> _classifiedMovesForCurrent = new();
	private readonly MoveClassificationEngine _classifier;

	private readonly object _cacheLock = new();

	private readonly PonderEngine    _ponder;
	private readonly QuickInfoEngine _quick;
	private readonly TimeSpan        _ponderBestMoveInterval;

	private CancellationTokenSource? _classificationCts;

	private volatile int _ponderPulseActiveFlag;

	private string? _currentPositionKey;
	private string? _lastPonderKey;
	private string? _latestPonderBest;
	private string? _latestPonderPonder;
	private Timer?  _ponderPulseTimer;

	public event Action<string>?             MoveClassificationCompleted;
	public event Action<string, Move>?       MoveClassified;
	public event Action<string, string>?     PonderBestMove;
	public event Action<PrincipalVariation>? PonderInfo;

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

		_ponder.InfoPv   += OnPonderInfo;
		_ponder.BestMove += (b, p) => PonderBestMove?.Invoke(b, p);
	}

	public IAsyncEnumerable<Move> ClassifyMovesAsync(
		Fen               fen,
		uint              perMoveDepth = 6,
		CancellationToken ct           = default)
		=> _classifier.ClassifyAsync(fen, perMoveDepth, ct);

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
	}

	public async Task StartAsync(CancellationToken ct = default)
	{
		await Task.WhenAll(
			_quick.StartAsync(ct),
			_ponder.StartAsync(ct),
			_classifier.StartAsync(ct)
		).ConfigureAwait(false);

		// Clear cached state at the start of a session
		_ = CaptureAndClearStateLocked(true);
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
			}
		}

		// Start or refresh periodic PonderBestMove pulses without restarting the search
		StartPonderPulseTimer();

		// If already pondering the same position, don't restart the engine search
		return identical ? Task.CompletedTask : _ponder.StartPonderAsync(fen, playedMoves, ct);
	}

	public async Task StopAsync(CancellationToken ct = default)
	{
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
		string key = BuildPositionKey(fen, playedMoves);
		lock (_cacheLock)
		{
			if (string.Equals(_lastPonderKey, key, StringComparison.Ordinal))
				// No change in position; keep current pondering/classification
				return;
		}

		// Stop previous pondering
		await StopPonderAsync(ct).ConfigureAwait(false);

		// Prepare new classification context (with linked CTS)
		PrepareNewClassificationContext(key, ct, out var oldCts, out var localCts);

		// Cancel prior classification (outside lock)
		oldCts?.Cancel();
		oldCts?.Dispose();

		// Ensure quick engine is at the new position for fast legal moves
		await _quick.SetPositionAsync(fen, playedMoves, ct).ConfigureAwait(false);

		// Start pondering for the new position (fire-and-forget)
		_ = StartPonderAsync(fen, playedMoves, ct);

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
		PonderInfo?.Invoke(pv);
		// Capture the latest best/ponder moves from the PV
		lock (_cacheLock)
		{
			if (pv.Moves is { Count: > 0 })
			{
				_latestPonderBest   = pv.Moves[0];
				_latestPonderPonder = pv.Moves.Count > 1 ? pv.Moves[1] : string.Empty;
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
