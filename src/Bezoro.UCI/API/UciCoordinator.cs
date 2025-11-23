using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Engines;

namespace Bezoro.UCI.API;

/// <summary>
/// Coordinates multiple UCI engines for fast information, pondering, and full move classification.
/// Orchestrates updating positions, synchronized pondering, background classification, and emits unified events updating UI or consumers.
/// </summary>
public sealed class UciCoordinator : IAsyncDisposable
{
	/// <summary>
	/// Internal table of per-move classifications for the current position, keyed by move notation.
	/// </summary>
	private readonly Dictionary<string, Move> _classifiedMovesForCurrent = new();

	private Fen           _currentBaseFen = Fen.Default;
	private List<string>? _currentMoves;

	private readonly MoveClassificationEngine _classifier;
	private readonly SynchronizationContext?  _syncContext;
	private readonly object                   _sync = new();

	private readonly PonderEngine    _ponder;
	private readonly QuickInfoEngine _quick;

	private CancellationTokenSource? _bestCts;
	private CancellationTokenSource? _classificationCts;

	/// <summary>
	/// Raised when all legal moves for the current position have been classified.
	/// </summary>
	public event Action<IReadOnlyList<Move>>? AllMovesClassified;

	/// <summary>
	/// Raised when the set of legal moves is updated (e.g. on position change).
	/// </summary>
	public event Action<IReadOnlyCollection<string>>? LegalMovesUpdated;

	/// <summary>
	/// Raised each time a new legal move is classified.
	/// </summary>
	public event Action<string, Move>? NewMoveClassified;

	/// <summary>
	/// Raised when a move is explicitly made via <see cref="MakeMoveAsync"/>.
	/// </summary>
	public event Action<string>? MoveMade;

	/// <summary>
	///     Raised when a move is explicitly undone via <see cref="UndoLastMoveAsync" />.
	/// </summary>
	public event Action<string>? MoveUndone;

	/// <summary>
	/// Raised when the ponder engine reports a best move (with optional ponder).
	/// </summary>
	public event Action<ParsedMove, ParsedMove?>? PonderBestMove;

	/// <summary>
	/// Raised whenever the ponder engine emits a new principal variation (PV).
	/// </summary>
	public event Action<PrincipalVariation>? PonderInfo;

	/// <summary>
	/// Raised when the coordinator is fully initialized and ready.
	/// </summary>
	public event Action? Ready;

	/// <summary>
	///     Raised when the coordinator has fully stopped.
	/// </summary>
	public event Action? Stopped;

	/// <summary>
	///     Raised when a new game is started via <see cref="NewGameAsync" />.
	/// </summary>
	public event Action? NewGame;

	/// <summary>
	///     Raised when the starting position is set (i.e. a position with no moves played).
	/// </summary>
	public event Action? StartingPositionSet;

	/// <summary>
	///     Raised whenever the position is updated (including when moves are made).
	/// </summary>
	public event Action? PositionUpdated;

	/// <summary>
	/// Constructs a new UciCoordinator with engines initialized for the given enginePath, arguments, and working directory.
	/// </summary>

	/// <param name="workingDirectory">Optional working directory.</param>
	/// <param name="syncContext">Optional synchronization context to marshal events to (e.g. UI thread).</param>
	public UciCoordinator(
		string                  enginePath,
		IEnumerable<string>?    args             = null,
		string?                 workingDirectory = null,
		SynchronizationContext? syncContext      = null)
	{
		_syncContext = syncContext;
		_quick       = new(enginePath, args, workingDirectory);
		_ponder      = new(enginePath, args, workingDirectory);
		_classifier  = new(enginePath, args, workingDirectory);

		_ponder.InfoPv   += OnPonderInfo;
		_ponder.BestMove += PonderOnBestMove;

		_classifier.MoveClassified     += OnClassifierMoveClassified;
		_classifier.AllMovesClassified += OnClassifierAllMovesClassified;
	}

	/// <summary>
	/// The most recently determined legal moves for the current position, if known.
	/// </summary>
	public IReadOnlyCollection<string>? CurrentLegalMoves
	{
		get
		{
			lock (_sync)
			{
				return _currentLegalMoves;
			}
		}
		private set
		{
			lock (_sync)
			{
				_currentLegalMoves = value;
			}
		}
	}

	private IReadOnlyCollection<string>? _currentLegalMoves;


	/// <summary>
	/// Prepares a new game in all engines. Clears all cached move classifications and legal moves.
	/// </summary>
	public async Task NewGameAsync(CancellationToken ct = default)
	{
		await Task.WhenAll(
			_quick.NewGameAsync(ct),
			_ponder.NewGameAsync(ct),
			_classifier.NewGameAsync(ct)
		).ConfigureAwait(false);

		ClearState();

		lock (_sync)
		{
			_currentLegalMoves = null;
		}

		Raise(NewGame);
	}

	/// <summary>
	/// Starts all engines and resets state. Sets some recommended UCI options on the ponder engine.
	/// </summary>
	public async Task StartAsync(CancellationToken ct = default)
	{
		await Task.WhenAll(
			_quick.StartAsync(ct),
			_ponder.StartAsync(ct),
			_classifier.StartAsync(ct)
		).ConfigureAwait(false);

		// Configure ponder engine for multi-threading and single PV analysis as a default.
		await _ponder.SetOptionAsync("Threads", "2", ct).ConfigureAwait(false);
		await _ponder.SetOptionAsync("MultiPv", "1", ct).ConfigureAwait(false);

		// Clear cached state at the start of a session
		ClearState();
		lock (_sync)
		{
			_currentLegalMoves = null;
		}

		Raise(Ready);
	}

	/// <summary>
	/// Begins an infinite search using the ponder engine at the given or current position. Cancels any previous search.
	/// </summary>
	/// <param name="fen">Position to search from; if null, uses the engine's current FEN.</param>
	/// <param name="playedMoves">Optional played moves to append to the FEN.</param>
	/// <param name="ct">Cancellation token.</param>
	public async Task StartSearchAsync(
		Fen?                 fen         = null,
		IEnumerable<string>? playedMoves = null,
		CancellationToken    ct          = default)
	{
		// Atomically cancel and replace the old cancellation token source
		var oldCts = Interlocked.Exchange(ref _bestCts, CancellationTokenSource.CreateLinkedTokenSource(ct));
		try
		{
			oldCts?.Cancel();
		}
		catch
		{
			// Best-effort: ignore errors when canceling old CTS
		}
		finally
		{
			oldCts?.Dispose();
		}

		var effectiveFen = fen;
		if (!effectiveFen.HasValue)
		{
			effectiveFen = await _quick.GetCurrentFenAsync(_bestCts.Token).ConfigureAwait(false);
			if (!effectiveFen.HasValue)
			{
				var ctsToDispose = Interlocked.Exchange(ref _bestCts, null);
				ctsToDispose?.Dispose();
				return;
			}
		}

		await _ponder.StartSearchAsync(effectiveFen.Value, playedMoves, _bestCts.Token).ConfigureAwait(false);
	}

	/// <summary>
	/// Stops any ongoing search and all engine operations. Resets cached state and cancels in-flight move classification.
	/// </summary>
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

		lock (_sync)
		{
			_currentLegalMoves = null;
		}

		Raise(Stopped);
	}

	/// <summary>
	/// Stops any ongoing infinite ponder search.
	/// </summary>
	public async Task StopSearchAsync(CancellationToken ct = default)
	{
		// Atomically get and clear the cancellation token source
		var cts = Interlocked.Exchange(ref _bestCts, null);
		try
		{
			cts?.Cancel();
		}
		catch
		{
			// Best-effort: ignore errors when canceling
		}
		finally
		{
			cts?.Dispose();
		}

		try
		{
			await _ponder.StopSearchAsync(ct).ConfigureAwait(false);
		}
		catch
		{
			// Best-effort: Swallow any errors on stop.
		}
	}

	/// <summary>
	/// Fully updates the engines to reflect a new board position:
	///   - Stops prior search and cancels in-flight classification.
	///   - Updates all engines to the new position.
	///   - Publishes new set of legal moves via LegalMovesUpdated.
	///   - Kicks off ponder search and background classification.
	/// </summary>
	/// <param name="fen">Position FEN</param>
	/// <param name="playedMoves">Optional moves played after the FEN</param>
	/// <param name="ct">Cancellation token</param>
	public async Task UpdatePositionAsync(
		Fen                  fen,
		IEnumerable<string>? playedMoves,
		CancellationToken    ct = default)
	{
		// Stop previous searches and any ongoing classification
		await StopSearchAsync(ct).ConfigureAwait(false);
		_classifier.StopClassification();
		ClearState();

		lock (_sync)
		{
			_currentLegalMoves = null;
			_currentBaseFen    = fen;
			// Store a copy of the moves list to avoid external mutation issues
			_currentMoves = playedMoves?.ToList();
		}

		// Set the position and publish legal moves
		await _quick.SetPositionAsync(fen, playedMoves, ct).ConfigureAwait(false);
		// Keep ponder engine synchronized with the quick engine position
		await _ponder.SetPositionAsync(fen, playedMoves, ct).ConfigureAwait(false);
		var moves = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);
		lock (_sync)
		{
			_currentLegalMoves = moves;
		}


		Raise(LegalMovesUpdated, moves);
		Raise(PositionUpdated);

		if (playedMoves == null || !playedMoves.Any()) Raise(StartingPositionSet);

		// Determine effective FEN for completion notification
		var effectiveFen = await _quick.GetCurrentFenAsync(ct).ConfigureAwait(false);

		// Start pondering for the new position
		_ = StartSearchAsync(fen, playedMoves, ct);

		// Start background classification (events will propagate results)
		if (effectiveFen.HasValue)
		{
			// Create new cancellation token source for this classification task
			// (ClearState() already canceled and disposed the previous one)
			var oldClassificationCts = Interlocked.Exchange(ref _classificationCts, CancellationTokenSource.CreateLinkedTokenSource(ct));
			// Dispose any old CTS that might have been set concurrently (should be null after ClearState)
			oldClassificationCts?.Dispose();

			// Fire-and-forget move classifier (listeners handle events)
			_ = Task.Run(
				async () =>
				{
					try
					{
						await foreach (var _ in _classifier
												.ClassifyAsync(effectiveFen.Value, 6, _classificationCts.Token)
												.ConfigureAwait(false))
						{
							// No-op; MoveClassificationEngine events will handle updates
						}
					}
					catch
					{
						// Best-effort: ignore cancellation/errors
					}
				},
				CancellationToken.None);
		}
	}

	/// <summary>
	///     Applies a move to the current position and updates the engines.
	///     This is a convenience wrapper around <see cref="UpdatePositionAsync" />.
	/// </summary>
	/// <param name="move">The move to play (in UCI notation, e.g. "e2e4").</param>
	/// <param name="ct">Cancellation token.</param>
	public async Task MakeMoveAsync(string move, CancellationToken ct = default)
	{
		Fen           fen;
		List<string>? moves;

		lock (_sync)
		{
			fen   = _currentBaseFen;
			moves = _currentMoves != null ? new(_currentMoves) : new List<string>();
		}

		moves.Add(move);

		await UpdatePositionAsync(fen, moves, ct).ConfigureAwait(false);
		Raise(MoveMade, move);
	}

	/// <summary>
	///     Reverts the last move played, if any, and updates the engines.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>True if a move was undone; false if there were no moves to undo.</returns>
	public async Task<bool> UndoLastMoveAsync(CancellationToken ct = default)
	{
		Fen           fen;
		List<string>? moves;

		lock (_sync)
		{
			if (_currentMoves == null || _currentMoves.Count == 0)
				return false;

			fen   = _currentBaseFen;
			moves = new(_currentMoves);
		}

		string undoneMove = moves[moves.Count - 1];
		moves.RemoveAt(moves.Count - 1);

		await UpdatePositionAsync(fen, moves, ct).ConfigureAwait(false);
		Raise(MoveUndone, undoneMove);
		return true;
	}

	/// <summary>
	/// Gets the current FEN as seen by the quick info engine.
	/// </summary>
	public Task<Fen?> GetCurrentFenAsync(CancellationToken ct = default) =>
		_quick.GetCurrentFenAsync(ct);

	/// <summary>
	/// Gets the set of legal moves for the quick info engine's current position.
	/// </summary>
	public Task<IReadOnlyCollection<string>> GetLegalMovesAsync(CancellationToken ct = default) =>
		_quick.GetLegalMovesAsync(ct);

	/// <summary>
	/// Returns a snapshot of the fast-path legal moves with any available per-move classifications so far.
	/// Each move in the legal list is parsed, and matching Move records are returned as a dictionary.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>A MovesSnapshot containing parsed legal moves and associated classified moves.</returns>
	public async Task<MovesSnapshot> GetLegalMovesWithClassificationsAsync(CancellationToken ct = default)
	{
		var legalStrings = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);
		var legal        = ToParsedMoves(legalStrings);
		var classified   = SnapshotClassifiedMoves();
		return new(legal, classified);
	}

	/// <summary>
	/// Disposes all underlying engines and cancels outstanding classification.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		// Cancel any background classification
		ClearState();

		// Unsubscribe from engine events
		_ponder.InfoPv   -= OnPonderInfo;
		_ponder.BestMove -= PonderOnBestMove;

		_classifier.MoveClassified     -= OnClassifierMoveClassified;
		_classifier.AllMovesClassified -= OnClassifierAllMovesClassified;

		// Dispose cancellation token sources
		// (ClearState() already handled _classificationCts)
		var bestCts = Interlocked.Exchange(ref _bestCts, null);
		try
		{
			bestCts?.Cancel();
		}
		catch
		{
			// Best-effort: ignore errors when canceling
		}
		finally
		{
			bestCts?.Dispose();
		}

		await _classifier.DisposeAsync();
		await _ponder.DisposeAsync();
		await _quick.DisposeAsync();
	}

	/// <summary>
	/// Parses UCI move strings into ParsedMove objects.
	/// </summary>
	private static List<ParsedMove> ToParsedMoves(IReadOnlyCollection<string> legalStrings)
	{
		var legal = new List<ParsedMove>(legalStrings.Count);
		foreach (string s in legalStrings)
			legal.Add(ParsedMove.FromNotation(s));

		return legal;
	}

	/// <summary>
	/// Returns a snapshot of the current per-move classifications as a dictionary keyed by ParsedMove.
	/// Thread-safe.
	/// </summary>
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

	/// <summary>
	/// Cancels background move classification and clears all cached per-move results.
	/// </summary>
	private void ClearState()
	{
		_classifier.StopClassification();

		// Atomically get and clear the classification cancellation token source
		var oldClassificationCts = Interlocked.Exchange(ref _classificationCts, null);
		try
		{
			oldClassificationCts?.Cancel();
		}
		catch
		{
			// Best-effort: ignore errors when canceling
		}
		finally
		{
			oldClassificationCts?.Dispose();
		}

		lock (_sync)
		{
			_classifiedMovesForCurrent.Clear();
		}
	}

	/// <summary>
	/// Event handler for when all moves are classified for the current position.
	/// Filters out any moves that are not legal in the current game state.
	/// Propagates to <see cref="AllMovesClassified"/>.
	/// </summary>
	/// <param name="moves">The classified moves as reported by the classifier engine.</param>
	private void OnClassifierAllMovesClassified(IReadOnlyList<Move> moves)
	{
		// Filter out any moves that are not legal in the current position
		IReadOnlyCollection<string>? legal;
		lock (_sync)
		{
			legal = _currentLegalMoves;
		}
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


		Raise(AllMovesClassified, moves);
	}

	/// <summary>
	/// Event handler for when a single move is classified.
	/// Updates internal cache; only publishes if move is legal in the current position.
	/// Propagates to <see cref="NewMoveClassified"/>.
	/// </summary>
	/// <param name="move">The newly classified move.</param>
	private void OnClassifierMoveClassified(Move move)
	{
		// Only accept and publish classifications for legal moves in the current position
		IReadOnlyCollection<string>? legal;
		lock (_sync)
		{
			legal = _currentLegalMoves;
		}

		if (legal == null || !legal.Contains(move.Notation))
			return;

		lock (_sync)
		{
			_classifiedMovesForCurrent[move.Notation] = move;
		}

		Raise(NewMoveClassified, move.Notation, move);
	}

	/// <summary>
	/// Forwards ponder engine PV updates.
	/// </summary>
	private void OnPonderInfo(PrincipalVariation pv)
	{
		Raise(PonderInfo, pv);
	}

	/// <summary>
	/// Forwards the best move (and optional ponder move) found by the ponder engine.
	/// </summary>
	private void PonderOnBestMove(ParsedMove best, ParsedMove? ponder)
	{
		Raise(PonderBestMove, best, ponder);
	}

	private void Raise(Action? handler)
	{
		if (handler == null) return;

		if (_syncContext != null)
			_syncContext.Post(_ => handler(), null);
		else
			handler();
	}

	private void Raise<T>(Action<T>? handler, T args)
	{
		if (handler == null) return;

		if (_syncContext != null)
			_syncContext.Post(_ => handler(args), null);
		else
			handler(args);
	}

	private void Raise<T1, T2>(Action<T1, T2>? handler, T1 arg1, T2 arg2)
	{
		if (handler == null) return;

		if (_syncContext != null)
			_syncContext.Post(_ => handler(arg1, arg2), null);
		else
			handler(arg1, arg2);
	}
}
