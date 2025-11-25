using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Engines;

namespace Bezoro.UCI.API;

/// <summary>
///     Coordinates multiple UCI engines for fast information, pondering, and full move classification.
///     Orchestrates updating positions, synchronized pondering, background classification, and emits unified events
///     updating UI or consumers.
/// </summary>
public sealed class UciCoordinator : IAsyncDisposable
{
	private readonly MoveClassificationEngine _classifier;
	private readonly object                   _sync = new();

	private readonly PonderEngine            _ponder;
	private readonly QuickInfoEngine         _quick;
	private readonly SynchronizationContext? _syncContext;

	private CancellationTokenSource? _bestCts;
	private CancellationTokenSource? _classificationCts;

	private UciState _state = UciState.Default;

	/// <summary>
	///     Raised when the coordinator is fully initialized and ready.
	/// </summary>
	public event Action? Ready;

	/// <summary>
	///     Raised when the coordinator state changes.
	/// </summary>
	public event Action<UciState>? StateChanged;

	/// <summary>
	///     Raised when the coordinator has fully stopped.
	/// </summary>
	public event Action? Stopped;

	/// <summary>
	///     Constructs a new UciCoordinator with engines initialized for the given enginePath, arguments, and working
	///     directory.
	/// </summary>
	/// <param name="enginePath">Path to the UCI engine executable.</param>
	/// <param name="args">Optional arguments for the engine.</param>
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
	///     Gets the current state of the coordinator.
	/// </summary>
	public UciState State
	{
		get
		{
			lock (_sync)
			{
				return _state;
			}
		}
		private set
		{
			lock (_sync)
			{
				_state = value;
			}

			Raise(StateChanged, value);
		}
	}

	/// <summary>
	///     Streams classified moves for the current position.
	///     Yields already classified moves first, then waits for new ones.
	///     Completes when all moves for the current position are classified.
	/// </summary>
	public async IAsyncEnumerable<Move> StreamClassifiedMovesAsync(
		[EnumeratorCancellation] CancellationToken ct = default)
	{
		// Capture current state
		UciState initialState;
		lock (_sync)
		{
			initialState = _state;
		}

		// Yield already classified moves
		foreach (var kvp in initialState.ClassifiedMoves) yield return kvp.Value;

		var channel = Channel.CreateUnbounded<Move>();

		void OnStateChanged(UciState newState)
		{
			// If the position changed (Fen or PlayedMoves), we stop
			if (newState.Fen != initialState.Fen || !newState.PlayedMoves.SequenceEqual(initialState.PlayedMoves))
				channel.Writer.TryComplete();
		}

		void OnMoveClassified(Move m)
		{
			channel.Writer.TryWrite(m);
		}

		void OnAllClassified(IReadOnlyList<Move> _)
		{
			channel.Writer.TryComplete();
		}

		_classifier.MoveClassified     += OnMoveClassified;
		_classifier.AllMovesClassified += OnAllClassified;
		StateChanged                   += OnStateChanged;

		try
		{
			// We need to filter duplicates that we already yielded
			var yielded = new HashSet<string>(initialState.ClassifiedMoves.Keys);

			while (await channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
			{
				while (channel.Reader.TryRead(out var move))
				{
					if (yielded.Add(move.Notation))
						yield return move;
				}
			}
		}
		finally
		{
			_classifier.MoveClassified     -= OnMoveClassified;
			_classifier.AllMovesClassified -= OnAllClassified;
			StateChanged                   -= OnStateChanged;
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
		UciState currentState;
		lock (_sync)
		{
			currentState = _state;
		}

		var newMoves = new List<string>(currentState.PlayedMoves) { move };
		await UpdatePositionAsync(currentState.Fen, newMoves, ct).ConfigureAwait(false);
	}

	/// <summary>
	///     Prepares a new game in all engines. Clears all cached move classifications and legal moves.
	/// </summary>
	public async Task NewGameAsync(CancellationToken ct = default)
	{
		await Task.WhenAll(
			_quick.NewGameAsync(ct),
			_ponder.NewGameAsync(ct),
			_classifier.NewGameAsync(ct)
		).ConfigureAwait(false);

		ClearState();
		State = UciState.Default;
	}

	/// <summary>
	///     Starts all engines and resets state. Sets some recommended UCI options on the ponder engine.
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

		ClearState();
		State = UciState.Default;

		Raise(Ready);
	}

	/// <summary>
	///     Begins an infinite search using the ponder engine at the given or current position. Cancels any previous search.
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

		lock (_sync)
		{
			_state = _state with { IsSearching = true };
		}

		Raise(StateChanged, _state);

		await _ponder.StartSearchAsync(effectiveFen.Value, playedMoves, _bestCts.Token).ConfigureAwait(false);
	}

	/// <summary>
	///     Stops any ongoing search and all engine operations. Resets cached state and cancels in-flight move classification.
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

		ClearState();
		State = UciState.Default;

		Raise(Stopped);
	}

	/// <summary>
	///     Stops any ongoing infinite ponder search.
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

		lock (_sync)
		{
			_state = _state with { IsSearching = false };
		}

		Raise(StateChanged, _state);
	}

	/// <summary>
	///     Fully updates the engines to reflect a new board position:
	///     - Stops prior search and cancels in-flight classification.
	///     - Updates all engines to the new position.
	///     - Publishes new set of legal moves via StateChanged.
	///     - Kicks off ponder search and background classification.
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

		var movesList = playedMoves?.ToList() ?? new List<string>();

		// Set the position and publish legal moves
		await _quick.SetPositionAsync(fen, movesList, ct).ConfigureAwait(false);
		// Keep ponder engine synchronized with the quick engine position
		await _ponder.SetPositionAsync(fen, movesList, ct).ConfigureAwait(false);

		var legalMoves = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);

		lock (_sync)
		{
			_state = new(
				fen,
				movesList.ToImmutableList(),
				legalMoves.ToImmutableList(),
				ImmutableDictionary<string, Move>.Empty,
				null,
				null,
				null,
				false
			);
		}

		Raise(StateChanged, _state);

		// Determine effective FEN for completion notification
		var effectiveFen = await _quick.GetCurrentFenAsync(ct).ConfigureAwait(false);

		// Start pondering for the new position
		_ = StartSearchAsync(fen, movesList, ct);

		// Start background classification (events will propagate results)
		if (effectiveFen.HasValue)
		{
			// Create new cancellation token source for this classification task
			// (ClearState() already canceled and disposed the previous one)
			var oldClassificationCts = Interlocked.Exchange(
				ref _classificationCts,
				CancellationTokenSource.CreateLinkedTokenSource(ct));

			// Dispose any old CTS that might have been set concurrently (should be null after ClearState)
			oldClassificationCts?.Dispose();

			// Fire-and-forget move classifier (listeners handle events)
			_ = Task.Run(
				async () =>
				{
					try
					{
						await foreach (var _ in _classifier
												.ClassifyAsync(
													effectiveFen.Value,
													6,
													legalMoves,
													_classificationCts.Token)
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
	///     Reverts the last move played, if any, and updates the engines.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>True if a move was undone; false if there were no moves to undo.</returns>
	public async Task<bool> UndoLastMoveAsync(CancellationToken ct = default)
	{
		UciState currentState;
		lock (_sync)
		{
			currentState = _state;
		}

		if (currentState.PlayedMoves.Count == 0)
			return false;

		var newMoves = new List<string>(currentState.PlayedMoves);
		newMoves.RemoveAt(newMoves.Count - 1);

		await UpdatePositionAsync(currentState.Fen, newMoves, ct).ConfigureAwait(false);
		return true;
	}

	/// <summary>
	///     Gets the current FEN as seen by the quick info engine.
	/// </summary>
	public Task<Fen?> GetCurrentFenAsync(CancellationToken ct = default) =>
		_quick.GetCurrentFenAsync(ct);

	/// <summary>
	///     Disposes all underlying engines and cancels outstanding classification.
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
	///     Cancels background move classification and clears all cached per-move results.
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
	}

	/// <summary>
	///     Event handler for when all moves are classified for the current position.
	///     Filters out any moves that are not legal in the current game state.
	/// </summary>
	private void OnClassifierAllMovesClassified(IReadOnlyList<Move> moves)
	{
		// We don't strictly need to do anything here for the state,
		// as individual moves are added as they come in.
		// But we might want to ensure consistency.
	}

	/// <summary>
	///     Event handler for when a single move is classified.
	///     Updates internal cache; only publishes if move is legal in the current position.
	/// </summary>
	private void OnClassifierMoveClassified(Move move)
	{
		lock (_sync)
		{
			// Verify relevance
			if (!_state.LegalMoves.Contains(move.Notation)) return;

			var newClassified = _state.ClassifiedMoves.SetItem(move.Notation, move);

			_state = _state with { ClassifiedMoves = newClassified };
		}

		Raise(StateChanged, _state);
	}

	/// <summary>
	///     Forwards ponder engine PV updates.
	/// </summary>
	private void OnPonderInfo(PrincipalVariation pv)
	{
		lock (_sync)
		{
			_state = _state with { Evaluation = pv };
		}

		Raise(StateChanged, _state);
	}

	/// <summary>
	///     Forwards the best move (and optional ponder move) found by the ponder engine.
	/// </summary>
	private void PonderOnBestMove(ParsedMove best, ParsedMove? ponder)
	{
		lock (_sync)
		{
			_state = _state with { BestMove = best, PonderMove = ponder };
		}

		Raise(StateChanged, _state);
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
}
