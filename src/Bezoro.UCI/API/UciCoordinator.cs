using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Engines;

namespace Bezoro.UCI.API;

public sealed class UciCoordinator : IAsyncDisposable
{
	private readonly Dictionary<string, Move> _classifiedMovesForCurrent = new();

	private readonly MoveClassificationEngine _classifier;
	private readonly object                   _sync = new();

	private readonly PonderEngine    _ponder;
	private readonly QuickInfoEngine _quick;

	private CancellationTokenSource?          _bestCts;
	public event Action<IReadOnlyList<Move>>? AllMovesClassified;

	public event Action<IReadOnlyCollection<string>>? LegalMovesUpdated;
	public event Action<string, Move>?                NewMoveClassified;
	public event Action<ParsedMove, ParsedMove?>?     PonderBestMove;
	public event Action<PrincipalVariation>?          PonderInfo;

	public UciCoordinator(
		string               enginePath,
		IEnumerable<string>? args             = null,
		string?              workingDirectory = null)
	{
		_quick      = new(enginePath, args, workingDirectory);
		_ponder     = new(enginePath, args, workingDirectory);
		_classifier = new(enginePath, args, workingDirectory);

		_ponder.InfoPv   += OnPonderInfo;
		_ponder.BestMove += PonderOnBestMove;

		_classifier.MoveClassified     += OnClassifierMoveClassified;
		_classifier.AllMovesClassified += OnClassifierAllMovesClassified;
	}

	public IReadOnlyCollection<string>? CurrentLegalMoves { get; private set; }


	public async Task NewGameAsync(CancellationToken ct = default)
	{
		await Task.WhenAll(
			_quick.NewGameAsync(ct),
			_ponder.NewGameAsync(ct),
			_classifier.NewGameAsync(ct)
		).ConfigureAwait(false);

		ClearState();

		CurrentLegalMoves = null;
	}

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
		ClearState();
		CurrentLegalMoves = null;
	}

	// Starts a unified infinite search via the PonderEngine using either the provided FEN or the engine's current FEN.
	public async Task StartSearchAsync(
		Fen?                 fen         = null,
		IEnumerable<string>? playedMoves = null,
		CancellationToken    ct          = default)
	{
		_bestCts?.Cancel();
		_bestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

		var effectiveFen = fen;
		if (!effectiveFen.HasValue)
		{
			effectiveFen = await _quick.GetCurrentFenAsync(_bestCts.Token).ConfigureAwait(false);
			if (!effectiveFen.HasValue) return;
		}

		await _ponder.StartSearchAsync(effectiveFen.Value, playedMoves, _bestCts.Token).ConfigureAwait(false);
	}

	public async Task StopAsync(CancellationToken ct = default)
	{
		// Stop searches first
		await StopSearchAsync(ct).ConfigureAwait(false);

		// Stop engines
		await Task.WhenAll(
			_classifier.StopAsync(ct),
			_ponder.StopAsync(ct),
			_quick.StopAsync(ct)
		).ConfigureAwait(false);

		// Clear cached state on stop and cancel in-flight classification
		ClearState();

		CurrentLegalMoves = null;
	}

	// Stops any ongoing search.
	public async Task StopSearchAsync(CancellationToken ct = default)
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

	public async Task UpdatePositionAsync(
		Fen                  fen,
		IEnumerable<string>? playedMoves,
		CancellationToken    ct = default)
	{
		// Stop previous searches and any ongoing classification
		await StopSearchAsync(ct).ConfigureAwait(false);
		_classifier.StopClassification();
		ClearState();

		// Set the position and publish legal moves
		await _quick.SetPositionAsync(fen, playedMoves, ct).ConfigureAwait(false);
		// Keep ponder engine synchronized with the quick engine position
		await _ponder.SetPositionAsync(fen, playedMoves, ct).ConfigureAwait(false);
		var moves = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);
		CurrentLegalMoves = moves;
		LegalMovesUpdated?.Invoke(moves);

		// Determine effective FEN for completion notification
		var effectiveFen = await _quick.GetCurrentFenAsync(ct).ConfigureAwait(false);
		effectiveFen?.ToString();

		// Start pondering for the new position
		_ = StartSearchAsync(fen, playedMoves, ct);

		// Start background classification (events will propagate results)
		if (effectiveFen.HasValue)
		{
			_ = Task.Run(
				async () =>
				{
					try
					{
						await foreach (var _ in _classifier
												.ClassifyAsync(effectiveFen.Value, 6, ct)
												.ConfigureAwait(false))
						{
							// No-op; MoveClassificationEngine events will handle updates
						}
					}
					catch
					{
						// best-effort: ignore cancellation/errors
					}
				},
				CancellationToken.None);
		}
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
		// Cancel any background classification
		ClearState();

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

	private IReadOnlyDictionary<ParsedMove, Move> SnapshotClassifiedMoves()
	{
		Dictionary<ParsedMove, Move> dict;
		lock (_sync)
		{
			dict = new(_classifiedMovesForCurrent.Count);
			foreach (var kvp in _classifiedMovesForCurrent)
				dict[ParsedMove.FromNotation(kvp.Key)] = kvp.Value;
		}

		return dict;
	}

	private void ClearState()
	{
		_classifier.StopClassification();

		lock (_sync)
		{
			_classifiedMovesForCurrent.Clear();
		}
	}

	private void OnClassifierAllMovesClassified(IReadOnlyList<Move> moves)
	{
		// Filter out any moves that are not legal in the current position
		var legal = CurrentLegalMoves;
		if (legal != null)
		{
			var filtered = new List<Move>(moves.Count);
			foreach (var m in moves)
			{
				if (legal.Contains(m.Notation))
					filtered.Add(m);
			}

			moves = filtered;
		}

		AllMovesClassified?.Invoke(moves);
	}

	private void OnClassifierMoveClassified(Move move)
	{
		// Only accept and publish classifications for legal moves in the current position
		var legal = CurrentLegalMoves;
		if (legal != null && !legal.Contains(move.Notation))
			return;

		lock (_sync)
		{
			_classifiedMovesForCurrent[move.Notation] = move;
		}

		NewMoveClassified?.Invoke(move.Notation, move);
	}

	private void OnPonderInfo(PrincipalVariation pv)
	{
		PonderInfo?.Invoke(pv);
	}

	private void PonderOnBestMove(ParsedMove best, ParsedMove? ponder)
	{
		PonderBestMove?.Invoke(best, ponder);
	}
}
