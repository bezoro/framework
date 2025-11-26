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
public sealed class UciCoordinator : IAsyncDisposable, IDisposable
{
	private readonly MoveClassificationEngine _classifier;
	private readonly object                   _sync = new();

	private readonly UciCoordinatorOptions   _options;
	private readonly PonderEngine            _ponder;
	private readonly QuickInfoEngine         _quick;
	private readonly SynchronizationContext? _syncContext;

	private CancellationTokenSource? _bestCts;
	private CancellationTokenSource? _classificationCts;

	private UciState _state = UciState.Default;

	/// <summary>
	///     Raised when an error occurs in any of the underlying engines.
	/// </summary>
	public event Action<Exception>? Error;

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
	///     Creates and starts a new UciCoordinator with engines initialized and ready.
	/// </summary>
	/// <param name="enginePath">Path to the UCI engine executable.</param>
	/// <param name="options">Configuration options for the coordinator.</param>
	/// <param name="syncContext">Optional synchronization context to marshal events to (e.g. UI thread).</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>A fully initialized and started coordinator.</returns>
	public static async Task<UciCoordinator> CreateAsync(
		string                  enginePath,
		UciCoordinatorOptions?  options     = null,
		SynchronizationContext? syncContext = null,
		CancellationToken       ct          = default)
	{
		var coordinator = new UciCoordinator(enginePath, null, null, syncContext, options);
		await coordinator.StartAsync(ct).ConfigureAwait(false);
		return coordinator;
	}

	/// <summary>
	///     Constructs a new UciCoordinator with engines initialized for the given enginePath, arguments, and working
	///     directory.
	/// </summary>
	/// <param name="enginePath">Path to the UCI engine executable.</param>
	/// <param name="args">Optional arguments for the engine.</param>
	/// <param name="workingDirectory">Optional working directory.</param>
	/// <param name="syncContext">Optional synchronization context to marshal events to (e.g. UI thread).</param>
	/// <param name="options">Optional configuration options for the coordinator.</param>
	public UciCoordinator(
		string                  enginePath,
		IEnumerable<string>?    args             = null,
		string?                 workingDirectory = null,
		SynchronizationContext? syncContext      = null,
		UciCoordinatorOptions?  options          = null)
	{
		_syncContext = syncContext;
		_options     = options ?? UciCoordinatorOptions.Default;
		_quick       = new(enginePath, args, workingDirectory);
		_ponder      = new(enginePath, args, workingDirectory);
		_classifier  = new(enginePath, args, workingDirectory);

		_ponder.InfoPv   += OnPonderInfo;
		_ponder.BestMove += PonderOnBestMove;

		_classifier.MoveClassified     += OnClassifierMoveClassified;
		_classifier.AllMovesClassified += OnClassifierAllMovesClassified;
	}

	/// <summary>
	///     Gets the current position FEN (after all played moves).
	/// </summary>
	public Fen CurrentFen => State.CurrentFen;

	/// <summary>
	///     Gets the current evaluation from the ponder engine.
	/// </summary>
	public PrincipalVariation? Evaluation => State.Evaluation;

	/// <summary>
	///     Gets a value indicating whether all underlying engines are healthy and responsive.
	/// </summary>
	public bool IsHealthy => _quick.IsHealthy && _ponder.IsHealthy;

	/// <summary>
	///     Gets a value indicating whether all underlying engines have been started.
	/// </summary>
	public bool IsStarted => _quick.IsStarted && _ponder.IsStarted;

	/// <summary>
	///     Gets the list of legal moves in the current position.
	/// </summary>
	public IReadOnlyList<string> LegalMoves => State.LegalMoves;

	/// <summary>
	///     Gets the configuration options for this coordinator.
	/// </summary>
	public UciCoordinatorOptions Options => _options;

	/// <summary>
	///     Gets the list of moves played from the starting FEN.
	/// </summary>
	public IReadOnlyList<string> PlayedMoves => State.PlayedMoves;

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
	///     Streams state changes as an async enumerable.
	///     Yields the current state immediately, then yields each subsequent state change.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	public async IAsyncEnumerable<UciState> StreamStateAsync(
		[EnumeratorCancellation] CancellationToken ct = default)
	{
		var channel = Channel.CreateUnbounded<UciState>();

		void OnStateChanged(UciState state)
		{
			channel.Writer.TryWrite(state);
		}

		StateChanged += OnStateChanged;

		try
		{
			// Yield current state first
			yield return State;

			while (await channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
			{
				while (channel.Reader.TryRead(out var state))
					yield return state;
			}
		}
		finally
		{
			StateChanged -= OnStateChanged;
			channel.Writer.TryComplete();
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
			if (newState.BaseFen != initialState.BaseFen ||
				!newState.PlayedMoves.SequenceEqual(initialState.PlayedMoves))
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
	/// <returns>The new state after the move is applied.</returns>
	/// <exception cref="ArgumentException">Thrown when the move is not legal in the current position.</exception>
	public async Task<UciState> MakeMoveAsync(string move, CancellationToken ct = default)
	{
		UciState currentState;
		lock (_sync)
		{
			currentState = _state;
		}

		if (!currentState.LegalMoves.Contains(move))
			throw new ArgumentException($"Move '{move}' is not legal in the current position.", nameof(move));

		var newMoves = new List<string>(currentState.PlayedMoves) { move };
		await UpdatePositionAsync(currentState.BaseFen, newMoves, ct).ConfigureAwait(false);
		return State;
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
	///     Resets to the starting chess position.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	public Task ResetAsync(CancellationToken ct = default) =>
		UpdatePositionAsync(Fen.Default, null, ct);

	/// <summary>
	///     Sets the position from a FEN string.
	/// </summary>
	/// <param name="fen">The FEN string representing the position.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <exception cref="ArgumentException">Thrown when the FEN string is invalid.</exception>
	public Task SetPositionAsync(string fen, CancellationToken ct = default)
	{
		var parsed = Fen.Parse(fen);
		if (!parsed.HasValue)
			throw new ArgumentException($"Invalid FEN string: '{fen}'", nameof(fen));

		return UpdatePositionAsync(parsed.Value, null, ct);
	}

	/// <summary>
	///     Starts all engines and resets state. Sets UCI options on the ponder engine based on configuration.
	/// </summary>
	public async Task StartAsync(CancellationToken ct = default)
	{
		await Task.WhenAll(
			_quick.StartAsync(ct),
			_ponder.StartAsync(ct),
			_classifier.StartAsync(ct)
		).ConfigureAwait(false);

		// Configure ponder engine based on options.
		await _ponder.SetOptionAsync("Threads", _options.PonderThreads.ToString(), ct).ConfigureAwait(false);
		await _ponder.SetOptionAsync("MultiPv", _options.MultiPv.ToString(), ct).ConfigureAwait(false);

		ClearState();
		State = UciState.Default;

		Raise(Ready);
	}

	/// <summary>
	///     Begins an infinite search from the current position. Cancels any previous search.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	public Task StartSearchAsync(CancellationToken ct = default) =>
		StartSearchAsync(CurrentFen, null, ct);

	/// <summary>
	///     Begins an infinite search from the specified position. Cancels any previous search.
	/// </summary>
	/// <param name="fen">Position to search from.</param>
	/// <param name="playedMoves">Optional played moves to append to the FEN.</param>
	/// <param name="ct">Cancellation token.</param>
	public async Task StartSearchAsync(
		Fen                  fen,
		IEnumerable<string>? playedMoves = null,
		CancellationToken    ct          = default)
	{
		// Atomically cancel and replace the old cancellation token source
		CancelAndDispose(ref _bestCts, CancellationTokenSource.CreateLinkedTokenSource(ct));

		lock (_sync)
		{
			_state = _state with { IsSearching = true };
		}

		Raise(StateChanged, _state);

		await _ponder.StartSearchAsync(fen, playedMoves, _bestCts!.Token).ConfigureAwait(false);
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
		CancelAndDispose(ref _bestCts);

		try
		{
			await _ponder.StopSearchAsync(ct).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected when cancellation is requested
		}
		catch (Exception ex)
		{
			RaiseError(ex);
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

		// Get the effective FEN (actual position after moves)
		var effectiveFen = await _quick.GetCurrentFenAsync(ct).ConfigureAwait(false);

		lock (_sync)
		{
			_state = new(
				fen,
				effectiveFen ?? fen,
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

		// Start pondering for the new position
		_ = StartSearchAsync(fen, movesList, ct);

		// Start background classification (events will propagate results)
		if (effectiveFen is { } currentFen)
		{
			// Create new cancellation token source for this classification task
			// (ClearState() already canceled and disposed the previous one)
			CancelAndDispose(ref _classificationCts, CancellationTokenSource.CreateLinkedTokenSource(ct));

			// Fire-and-forget move classifier (listeners handle events)
			_ = Task.Run(
				async () =>
				{
					try
					{
						await foreach (var _ in _classifier
												.ClassifyAsync(
													currentFen,
													_options.ClassificationDepth,
													legalMoves,
													_classificationCts!.Token)
												.ConfigureAwait(false))
						{
							// No-op; MoveClassificationEngine events will handle updates
						}
					}
					catch (OperationCanceledException)
					{
						// Expected when position changes or coordinator stops
					}
					catch (Exception ex)
					{
						RaiseError(ex);
					}
				},
				CancellationToken.None);
		}
	}

	/// <summary>
	///     Reverts one or more moves and updates the engines.
	/// </summary>
	/// <param name="count">Number of moves to undo. Defaults to 1.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The new state after undoing moves, or the current state if no moves were undone.</returns>
	public async Task<UciState> UndoAsync(int count = 1, CancellationToken ct = default)
	{
		if (count < 1)
			throw new ArgumentOutOfRangeException(nameof(count), "Count must be at least 1.");

		UciState currentState;
		lock (_sync)
		{
			currentState = _state;
		}

		if (currentState.PlayedMoves.Count == 0)
			return currentState;

		int movesToRemove = Math.Min(count, currentState.PlayedMoves.Count);
		var newMoves      = currentState.PlayedMoves.Take(currentState.PlayedMoves.Count - movesToRemove).ToList();

		await UpdatePositionAsync(currentState.BaseFen, newMoves, ct).ConfigureAwait(false);
		return State;
	}

	/// <summary>
	///     Gets the actual current FEN (after all played moves) as seen by the engine.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The current position FEN, or null if not available.</returns>
	public Task<Fen?> GetCurrentFenAsync(CancellationToken ct = default) =>
		_quick.GetCurrentFenAsync(ct);

	/// <summary>
	///     Performs a blocking search with the specified parameters and returns the result.
	/// </summary>
	/// <param name="parameters">Search parameters (depth, time, nodes, etc.).</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The search result containing the best move and evaluation.</returns>
	public Task<SearchResult> SearchAsync(SearchParameters parameters, CancellationToken ct = default) =>
		_quick.QuickEvalAsync(CurrentFen, parameters.Depth ?? _options.ClassificationDepth, ct);

	/// <summary>
	///     Sets a UCI option on the ponder engine.
	/// </summary>
	/// <param name="name">The option name.</param>
	/// <param name="value">The option value, or null to use the engine's default.</param>
	/// <param name="ct">Cancellation token.</param>
	public Task SetOptionAsync(string name, string? value, CancellationToken ct = default) =>
		_ponder.SetOptionAsync(name, value, ct);

	/// <summary>
	///     Classifies a single move in the current position.
	/// </summary>
	/// <param name="move">The move to classify (in UCI notation).</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The classified move with analysis.</returns>
	/// <exception cref="ArgumentException">Thrown when the move is not legal.</exception>
	public async Task<Move> ClassifyMoveAsync(string move, CancellationToken ct = default)
	{
		var currentState = State;

		if (!currentState.LegalMoves.Contains(move))
			throw new ArgumentException($"Move '{move}' is not legal in the current position.", nameof(move));

		// Check if already classified
		if (currentState.ClassifiedMoves.TryGetValue(move, out var existing))
			return existing;

		// Use the classifier to classify this specific move
		var result = await _classifier.ClassifyMoveAsync(CurrentFen, move, _options.ClassificationDepth, ct)
									  .ConfigureAwait(false);

		if (!result.HasValue)
			throw new InvalidOperationException($"Failed to classify move '{move}'.");

		return result.Value;
	}

	/// <summary>
	///     Waits until all legal moves in the current position have been classified.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>Dictionary of all classified moves.</returns>
	public async Task<IReadOnlyDictionary<string, Move>> WaitForClassificationAsync(CancellationToken ct = default)
	{
		var tcs = new TaskCompletionSource<IReadOnlyDictionary<string, Move>>(
			TaskCreationOptions.RunContinuationsAsynchronously);

		using var registration = ct.Register(() => tcs.TrySetCanceled(ct));

		void OnStateChanged(UciState state)
		{
			if (state.IsClassificationComplete)
				tcs.TrySetResult(state.ClassifiedMoves);
		}

		StateChanged += OnStateChanged;

		try
		{
			// Check if already complete
			var current = State;
			if (current.IsClassificationComplete)
				return current.ClassifiedMoves;

			return await tcs.Task.ConfigureAwait(false);
		}
		finally
		{
			StateChanged -= OnStateChanged;
		}
	}

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
		CancelAndDispose(ref _bestCts);

		await _classifier.DisposeAsync();
		await _ponder.DisposeAsync();
		await _quick.DisposeAsync();
	}

	/// <summary>
	///     Synchronously disposes all underlying engines and cancels outstanding classification.
	/// </summary>
	public void Dispose()
	{
		DisposeAsync().AsTask().GetAwaiter().GetResult();
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///     Cancels background move classification and clears all cached per-move results.
	/// </summary>
	private void ClearState()
	{
		_classifier.StopClassification();
		CancelAndDispose(ref _classificationCts);
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

	/// <summary>
	///     Raises the Error event with the specified exception.
	/// </summary>
	private void RaiseError(Exception ex) => Raise(Error, ex);

	/// <summary>
	///     Atomically cancels and disposes a CancellationTokenSource field, optionally replacing it.
	/// </summary>
	private static void CancelAndDispose(ref CancellationTokenSource? ctsField, CancellationTokenSource? replacement = null)
	{
		var old = Interlocked.Exchange(ref ctsField, replacement);
		try
		{
			old?.Cancel();
		}
		catch
		{
			// Best-effort: ignore errors when canceling
		}
		finally
		{
			old?.Dispose();
		}
	}
}
