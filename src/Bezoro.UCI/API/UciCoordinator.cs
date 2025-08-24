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

	private CancellationTokenSource? _classificationCts;
	private string?                  _currentPositionKey;
	private string?                  _lastPonderKey;
	public event Action<string>?     MoveClassificationCompleted;

	public event Action<string, Move>?       MoveClassified;
	public event Action<string, string>?     PonderBestMove;
	public event Action<PrincipalVariation>? PonderInfo;

	public UciCoordinator(string enginePath, IEnumerable<string>? args = null, string? workingDirectory = null)
	{
		_quick      = new(enginePath, args, workingDirectory);
		_ponder     = new(enginePath, args, workingDirectory);
		_classifier = new(enginePath, args, workingDirectory);

		_ponder.InfoPv   += pv => PonderInfo?.Invoke(pv);
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

		// Clear cached state for new game
		CancellationTokenSource? toCancel;
		lock (_cacheLock)
		{
			toCancel            = _classificationCts;
			_classificationCts  = null;
			_currentPositionKey = null;
			_classifiedMovesForCurrent.Clear();
			_lastPonderKey = null;
		}

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
		lock (_cacheLock)
		{
			_classificationCts  = null;
			_currentPositionKey = null;
			_classifiedMovesForCurrent.Clear();
			_lastPonderKey = null;
		}
	}

	public Task StartPonderAsync(Fen fen, IEnumerable<string>? playedMoves, CancellationToken ct = default)
	{
		string key = BuildPositionKey(fen, playedMoves);
		lock (_cacheLock)
		{
			if (string.Equals(_lastPonderKey, key, StringComparison.Ordinal))
				// Already pondering this position; skip restart
				return Task.CompletedTask;

			_lastPonderKey = key;
		}

		return _ponder.StartPonderAsync(fen, playedMoves, ct);
	}

	public async Task StopAsync(CancellationToken ct = default)
	{
		await Task.WhenAll(
			_classifier.StopAsync(ct),
			_ponder.StopAsync(ct),
			_quick.StopAsync(ct)
		).ConfigureAwait(false);

		// Clear cached state on stop
		CancellationTokenSource? toCancel = null;
		lock (_cacheLock)
		{
			toCancel            = _classificationCts;
			_classificationCts  = null;
			_currentPositionKey = null;
			_classifiedMovesForCurrent.Clear();
			_lastPonderKey = null;
		}

		toCancel?.Cancel();
		toCancel?.Dispose();
	}

	public async Task StopPonderAsync(CancellationToken ct = default)
	{
		await _ponder.StopPonderAsync(ct).ConfigureAwait(false);
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

		// Cancel any in-flight classification and reset cache for the new position
		CancellationTokenSource? oldCts;
		lock (_cacheLock)
		{
			oldCts = _classificationCts;
			var newCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			_classificationCts  = newCts;
			_currentPositionKey = key;
			_classifiedMovesForCurrent.Clear();
		}

		oldCts?.Cancel();
		oldCts?.Dispose();

		// Ensure quick engine is at the new position for fast legal moves
		await _quick.SetPositionAsync(fen, playedMoves, ct).ConfigureAwait(false);

		// Start pondering for the new position (fire-and-forget)
		_ = StartPonderAsync(fen, playedMoves, ct);

		// Kick off background classification pipeline
		var localCts = _classificationCts!;
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
							stillCurrent = string.Equals(_currentPositionKey, key, StringComparison.Ordinal) &&
										   !localCts.IsCancellationRequested;

							if (stillCurrent)
								_classifiedMovesForCurrent[move.Notation] = move;
						}

						if (stillCurrent)
							MoveClassified?.Invoke(key, move);
						else
							break;
					}

					bool fireCompleted;
					lock (_cacheLock)
					{
						fireCompleted = string.Equals(_currentPositionKey, key, StringComparison.Ordinal) &&
										!localCts.IsCancellationRequested;
					}

					if (fireCompleted)
						MoveClassificationCompleted?.Invoke(key);
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
		var legal        = new List<ParsedMove>(legalStrings.Count);
		foreach (string? s in legalStrings)
			legal.Add(ParsedMove.FromNotation(s));

		IReadOnlyDictionary<ParsedMove, Move> classified;
		lock (_cacheLock)
		{
			var dict = new Dictionary<ParsedMove, Move>(_classifiedMovesForCurrent.Count);
			foreach (var kvp in _classifiedMovesForCurrent)
				dict[ParsedMove.FromNotation(kvp.Key)] = kvp.Value;

			classified = dict;
		}

		return new(legal, classified);
	}

	public async ValueTask DisposeAsync()
	{
		// Cancel any background classification
		CancellationTokenSource? toCancel;
		lock (_cacheLock)
		{
			toCancel            = _classificationCts;
			_classificationCts  = null;
			_currentPositionKey = null;
			_classifiedMovesForCurrent.Clear();
			_lastPonderKey = null;
		}

		toCancel?.Cancel();
		toCancel?.Dispose();

		await _classifier.DisposeAsync();
		await _ponder.DisposeAsync();
		await _quick.DisposeAsync();
	}

	private static string BuildPositionKey(Fen fen, IEnumerable<string>? playedMoves)
	{
		string movesJoined = playedMoves is null ? string.Empty : string.Join(' ', playedMoves);
		return $"{fen}|{movesJoined}";
	}
}
